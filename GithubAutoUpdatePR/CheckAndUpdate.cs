using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Octokit;
using Branch = LibGit2Sharp.Branch;
using Repository = LibGit2Sharp.Repository;

namespace GithubAutoUpdatePR
{
    class CheckAndUpdate
    {
        readonly GitHubAccount _account;
        private readonly Identity _identity;
        private readonly PushOptions _pushOptions;

        public CheckAndUpdate(GitHubAccount account)
        {
            _account = account;
            var cres = new UsernamePasswordCredentials()
            {
                Username = _account.UserName,
                Password = _account.Token
            };


            LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = new CredentialsHandler(
            (url, usernameFromUrl, types) => cres);
            options.MergeOptions = new MergeOptions();

             _identity = new Identity(_account.UserName, _account.Email);
             _pushOptions = new PushOptions()
            {
                CredentialsProvider = new CredentialsHandler((url, usernameFromUrl, types) => cres),
            };
        }

        public async Task CheckPullRequest()
        {
            var client = new GitHubClient(new ProductHeaderValue("GithubAutoUpdatePR"));
            var tokenAuth = new Octokit.Credentials(_account.Token);
            client.Credentials = tokenAuth;

            var username = _account.UserName;

            var userAllReps = await client.Repository.GetAllForCurrent();

            foreach (var repository in userAllReps)
            {
                if (_account.IncludeRepository.Count != 0)
                {
                    if (!_account.IncludeRepository.Contains(repository.Name))
                    {
                        continue;
                    }
                }

                var fullGetRep = await client.Repository.Get(repository.Id);

                var parent = fullGetRep.Parent;
                if (parent == null)
                {
                    continue;
                }

                var parentUser = parent.Owner.Login;
                var parentName = parent.Name;

                
                var currRepPrs = await client.PullRequest.GetAllForRepository(parentUser, parentName,
                   new PullRequestRequest { State = ItemStateFilter.Open }, ApiOptions.None);


                var currUserOpenPrs = currRepPrs.Where(w => w.User.Login == username).ToList();

                if (currUserOpenPrs.Count > 0)
                {
                    Pull(fullGetRep);
                }

                foreach (var pr in currUserOpenPrs)
                {
                    PullAndRebaseAndPush(pr);
                }

            }
        }

        public void Pull(Octokit.Repository rep)
        {
            string reppath = Path.Combine(_account.TempRepPath, rep.Name);
            if (!Repository.IsValid(reppath))
            {
                Repository.Clone(rep.CloneUrl, reppath);
                using (var repo = new Repository(reppath))
                {
                    var upstream = repo.Network.Remotes.Add("upstream", rep.Parent.CloneUrl);
                    Commands.Fetch(repo, "upstream", new List<string>() { }, new FetchOptions(), null);  
                    Branch upstreamMaster = repo.Branches["upstream/master"];
                    Branch localMaster = repo.Branches["master"];
                    repo.Branches.Update(localMaster, b => b.TrackedBranch = upstreamMaster.CanonicalName);
                    var sign = new LibGit2Sharp.Signature(_account.UserName, _account.Email, new DateTimeOffset(DateTime.Now));
                    Commands.Pull(repo, sign, new PullOptions());
                }
            }
            else
            {
                using (var repo = new Repository(reppath))
                {
                    var branchMaster = repo.Branches["master"];

                    Commands.Checkout(repo, branchMaster);
                    var sign = new LibGit2Sharp.Signature(_account.UserName, _account.Email, new DateTimeOffset(DateTime.Now));
                    Commands.Pull(repo, sign, new PullOptions());
                }
            }
      
        }

        public  void PullAndRebaseAndPush(PullRequest pr)
        {
            string reppath = Path.Combine(_account.TempRepPath, pr.Base.Repository.Name);

            using (var repo = new Repository(reppath))
            {
                var branchMaster = repo.Branches["master"];
                var masterCommits = branchMaster.Commits.First();
                var orgin = repo.Network.Remotes["orgin"];

                var branchname = pr.Head.Ref;
                repo.Branches.Remove(branchname);

                Branch orginBranch = repo.Branches[$"refs/remotes/origin/{branchname}"];

                Branch localBranch = repo.CreateBranch(branchname, orginBranch.Tip);
                localBranch = Commands.Checkout(repo, localBranch);

                if (!localBranch.Commits.Contains(masterCommits))
                {
                    Console.WriteLine(branchname);
                    var rebaseOptions = new RebaseOptions();
                    var rb = repo.Rebase.Start(localBranch, branchMaster, null, _identity, rebaseOptions);
                    if (rb.Status != RebaseStatus.Complete)
                    {
                        repo.Rebase.Abort();
                        return;
                    }
                    Commands.Checkout(repo, branchname);
                    repo.Network.Push(orgin, $"+refs/heads/{branchname}:refs/heads/{branchname}", _pushOptions);
                }
            }
        }
    }
}
