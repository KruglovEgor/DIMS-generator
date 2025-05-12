using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DIMS.Models
{
    public class RedmineApiConfig
    {
        public string BaseUrl { get; set; }
        public string ApiKey { get; set; }
        public int RootProjectId { get; set; }
    }
}
