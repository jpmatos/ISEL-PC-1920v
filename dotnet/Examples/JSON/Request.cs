using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AsyncServerClient.JSON
{
/**
 * The type that represents a JSON request
 */
    public class Request
    {
        public String Method { get; set; }
        public Dictionary<String, String> Headers { get; set; }
        public JObject Payload { get; set; }

        public override String ToString()
        {
            return $"Method: {Method}, Headers: {Headers}, Payload: {Payload}";
        }
    }
}