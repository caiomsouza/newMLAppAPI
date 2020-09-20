using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using webapi.Models;
using System.Configuration;
using Azure.Storage.Blobs;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;
using Azure.Storage;
using System.Text;
using System.Net.Http;
using System.IO;

namespace webapi.Controllers
{
    //[Authorize]
    [ApiController]
    
    public class newMLAppController : ControllerBase
    {
        private IConfiguration configuration;
        public newMLAppController(IConfiguration iConfig)
        {
            configuration = iConfig;
        }
        [HttpGet]
        [Route("api/getClaims")]
        public  List<SecurityClaims> getClaims()
        {
            var loggedinuser = User;
            List<SecurityClaims> SCL = new List<SecurityClaims>();


            
            foreach(var y in loggedinuser.Identities)
            {
                foreach (var x in y.Claims)
                {
            
                SecurityClaims SC = new SecurityClaims();
                SC.Properties = x.Properties;
                SC.Issuer = x.Issuer;
                SC.OriginalIssuer = x.OriginalIssuer;
                SC.ClaimType = x.ValueType;
                SC.ClaimValue = x.Value;
                SC.Type = x.Type;
                

                SCL.Add(SC);
                    
                }

            
            }
            return SCL;

        }

        [HttpPost]
        [Route("api/saveAndAnalyzeMedia")]
        public async Task<string> SaveAndAnalyze([FromBody] MediaData MD){
            
            var claims = getClaims();
            var objectid = claims.Where( x => x.Type.ToLower() == "http://schemas.microsoft.com/identity/claims/objectidentifier").Single().ClaimValue;
            //var objectid = "test";
            var accountName = configuration.GetSection("adlsgen2").GetSection("accountName").Value;
            var accountKey = configuration.GetSection("adlsgen2").GetSection("accountKey").Value;
            
            StorageSharedKeyCredential sharedKeyCredential =new StorageSharedKeyCredential(accountName, accountKey);


            string dfsUri = "https://" + accountName + ".dfs.core.windows.net";

            var dataLakeServiceClient = new DataLakeServiceClient(new Uri(dfsUri), sharedKeyCredential);
            var mediaFileSystem  = dataLakeServiceClient.GetFileSystemClient("mediafiles");
            
            DataLakeDirectoryClient directoryClient = mediaFileSystem.GetDirectoryClient(objectid);
            await directoryClient.CreateIfNotExistsAsync();


            var fileClient = directoryClient.GetFileClient( Guid.NewGuid().ToString() + ".jpg");
            // convert string to stream
            byte[] byteArray =  Convert.FromBase64String(MD.Mediabase64);
            //byte[] byteArray = Encoding.ASCII.GetBytes(contents);
            MemoryStream stream = new MemoryStream(byteArray);

        


            var upload1 = await fileClient.UploadAsync(stream, overwrite:true);
            return (fileClient.Uri.ToString());
            
        }
    }
}
