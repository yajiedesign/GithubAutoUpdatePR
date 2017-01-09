using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;

namespace GithubAutoUpdatePR
{
    class GitHubAccount
    {
        public static List<GitHubAccount> CreateWithConfig(string configPath)
        {
            var ret = new List<GitHubAccount>();
            var parser = new FileIniDataParser();
            IniData datas = parser.ReadFile(configPath);

            foreach (var section in datas.Sections)
            {
                GitHubAccount newGitHubAccount = new GitHubAccount
                {
                    UserName = section.Keys["username"],
                    Token = section.Keys["token"],
                    TempRepPath = section.Keys["tempreppath"],
                    Email = section.Keys["email"],
                    IncludeRepository =
                        section.Keys["include"].Split(',').Select(s => s.Trim()).ToList().AsReadOnly()
                };
                ret.Add(newGitHubAccount);
            }

            return ret;
        }



        /// <summary>
        /// GitHub UserName
        /// </summary>
        public string UserName { get; private set; }
        /// <summary>
        /// Email
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// GitHub Token
        /// </summary>
        public string Token { get; private set; }
        /// <summary>
        /// store you temp repository path
        /// </summary>
        public string TempRepPath { get; private set; }
        /// <summary>
        /// Include Repository,if is empty,include all.
        /// </summary>
        public ReadOnlyCollection<string> IncludeRepository { get; private set; }
    }
}
