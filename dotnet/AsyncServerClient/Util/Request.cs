using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient.Util
{
    public class Request
    {
        public String Method { get; set; }
        public String Path { get; set; }
        public Dictionary<String, String> Headers { get; set; }
        public JObject Payload { get; set; }

        public override String ToString()
        {
            return $"Method: {Method}, Path: {Path}, Headers: {Headers}, Payload: {Payload}";
        }
    }
}