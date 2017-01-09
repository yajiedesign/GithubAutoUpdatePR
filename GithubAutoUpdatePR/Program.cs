using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Octokit;
using System.Threading;

namespace GithubAutoUpdatePR
{
    class Program
    {
        static void Main(string[] args)
        {
            var accounts = GitHubAccount.CreateWithConfig("config.ini");
            var updates = accounts.Select(s => new CheckAndUpdate(s)).ToList();
            G(updates);
            Console.ReadLine();
        }


        static async void G(List<CheckAndUpdate> updates)
        {
            while (true)
            {
                foreach (var update in updates)
                {

                    try
                    {
                        await update.CheckPullRequest();
                    }
                    catch (Exception e)
                    {

                        Console.WriteLine(e);
                    }
            
                }
                Thread.Sleep(1000 * 60 * 5);
            }
        }
    }
}
