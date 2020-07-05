using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient.Util
{
    public class Response
    {
        public int Status { get; set; }
        public Dictionary<String, String> Headers { get; set; }
        public JObject Payload { get; set; }

        public override String ToString()
        {
            if (Headers != null || Payload != null)
                return $"Status: {Status}, Headers: {Headers}, Payload: {Payload}";
            else
                return $"Status: {Status}";
        }
    }
}