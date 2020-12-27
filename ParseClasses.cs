using System;
using System.Collections.Generic;

namespace PolyTechFramework
{
    public class Author
    {
        public int id { get; set; }
        public string login { get; set; }
        public string full_name { get; set; }
        public string email { get; set; }
        public string avatar_url { get; set; }
        public string language { get; set; }
        public bool is_admin { get; set; }
        public string last_login { get; set; } // DateTime
        public string created { get; set; } // DateTime
        public string username { get; set; }
    }

    public class Asset
    {
        public int id { get; set; }
        public string name { get; set; }
        public int size { get; set; }
        public int download_count { get; set; }
        public string created_at { get; set; } // DateTime
        public string uuid { get; set; }
        public string browser_download_url { get; set; }
    }

    public class Release
    {
        public int id { get; set; }
        public string tag_name { get; set; }
        public string target_commitish { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public string created_at { get; set; } // DateTime
        public string published_at { get; set; } // DateTime
        public Author author { get; set; }
        public List<Asset> assets { get; set; }

        public System.Version GetVersion()
        {
            string parsed = this.tag_name.ToLower().Replace("v", "");
            return new System.Version(parsed);
        }
    }

    public class ModUpdate
    {
        public System.Version new_version { 
            get
            {
                return latest_release.GetVersion();
            }
        }
        public System.Version old_version
        {
            get
            {
                return mod.Info.Metadata.Version;
            }
        }
        public PolyTechMod mod;
        public Release latest_release;
        public ModUpdate(PolyTechMod mod, Release latest_release)
        {
            this.mod = mod;
            this.latest_release = latest_release;
        }
    }
}