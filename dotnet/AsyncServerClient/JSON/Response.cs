using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace AsyncServer.JSON
{
/**
 * The type that represents a JSON response
 */
    public class Response
    {
        public int Status { get; set; }
        public Dictionary<String, String> Headers { get; set; }
        public JObject Payload { get; set; }

        public override String ToString()
        {
            return $"Status: {Status}, Headers: {Headers}, Payload: {Payload}";
        }
    }
}