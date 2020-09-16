using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace webapi.Models{

    public class SecurityClaims{
        public IDictionary<string,string> Properties {get;set;}
        public string Issuer {get;set;}
        public string OriginalIssuer {get;set;}

        public string ClaimValue {get;set;}
        public string ClaimType {get;set;}

        public string Type {get;set;}
        


    }
}