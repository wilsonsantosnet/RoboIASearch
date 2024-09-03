using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoboIASearch
{
    public class WorkerConfig
    {
        public string ApiKey { get; set; }
        public string Endpoint { get; set; }
        public List<string> Microservicos { get; set; }
        public string Output { get; set; }

    }
}
