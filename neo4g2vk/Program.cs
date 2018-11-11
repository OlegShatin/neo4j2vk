using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver.V1;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.RequestParams;

namespace neo4g2vk
{
    class Program
    {
        static void Main(string[] args)
        {
            var token = ConfigurationManager.AppSettings["token"];
            var api = new VkApi();

            api.Authorize(new ApiAuthParams()
            {
                AccessToken = token
            });
            Console.WriteLine(api.Token);
            var allMyFriends = api.Friends.Get(new FriendsGetParams()
            {
                Order = FriendsOrder.Hints,
                Fields = ProfileFields.All
            });
            var map = allMyFriends.ToDictionary(user => user.Id);
            var topTen = allMyFriends.Take(10).ToList();
            
            IDriver driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic(ConfigurationManager.AppSettings["dbusername"], 
                ConfigurationManager.AppSettings["dbpassword"]));
            using (ISession session = driver.Session())
            {
                foreach (var user in topTen)
                {
                    session.Run(GetMergeStatementForUser(user));
                    var mutualFriendIds = api.Friends.GetMutual(new FriendsGetMutualParams()
                    {
                        Count = 10,
                        Order = FriendsOrder.Hints,
                        TargetUid = user.Id
                    });
                    foreach (var mutualFriendId in mutualFriendIds)
                    {
                        //insert mutual
                        session.Run(GetMergeStatementForUser(map[mutualFriendId]));
                        //insert friendship
                        session.Run(GetMergeForFriendship(user.Id, map[mutualFriendId].Id));
                    }
                }
            }
            driver.Dispose();

            Console.WriteLine("Finish");
            Console.ReadLine();
        }

        static string GetMergeForFriendship(long firstUserId, long secondUserId)
        {
            return "MATCH (usr1: User { id:" + firstUserId + " }),(usr2: User { id:" + secondUserId +
                   " }) MERGE (usr1) -[:KNOWS]-> (usr2) MERGE (usr2)-[:KNOWS]->(usr1)";
        }

        static string GetMergeStatementForUser(User user)
        {
            return "MERGE (usr: User { name: '" + user.FirstName + " " + user.LastName + "', id:" +
                   user.Id + " })";
        }
    }
}
