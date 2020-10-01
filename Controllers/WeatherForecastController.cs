using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Net.Http;
namespace webapi.Controllers
{
    
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
            
        }

        [HttpGet]
        [Route("api/testcall")]
        public async Task<string>  GetSomeStuff(){
                       var aadtenant = "72f988bf-86f1-41af-91ab-2d7cd011db47";
            var appid ="2058820a-4a94-474b-8d49-7dfffaf4a1c7" ;
            var appSecret = "SoRy6k.U8Ie6L5_.G-s7Yo44XM40N6.lNa";

//https://contoso.azurewebsites.net/.auth/login/aad/callback
            var app = ConfidentialClientApplicationBuilder.Create(appid)
                            .WithAuthority(AzureCloudInstance.AzurePublic, aadtenant)
                            .WithClientSecret(appSecret)
                            .Build();
            string[] scopes = new string[] { "api://2058820a-4a94-474b-8d49-7dfffaf4a1c7/callapi" };

            AuthenticationResult result = null;
            try
            {
            result = await app.AcquireTokenForClient(scopes)
                            .ExecuteAsync();
                            
            }
            catch(MsalServiceException ex)
            {
                return ex.Message;
            // Case when ex.Message contains:
            // AADSTS70011 Invalid scope. The scope has to be of the form "https://resourceUrl/.default"
            // Mitigation: change the scope to be as expected
            }
            Console.WriteLine(result.AccessToken);
            return result.AccessToken;
 

        }
        [HttpGet]
        public IEnumerable<WeatherForecast> Get()
        {

            var rng = new Random();
            var user = User;
            //Console.Write(user.Identity.Name);
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
