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
using Azure.Storage.Sas;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Core;

using System.Text.Json.Serialization;
namespace webapi.Controllers
{
    [Authorize]
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
        public async Task<AnalysisResponse> SaveAndAnalyze([FromBody] MediaData MD){
            
            try
            {
                var claims = getClaims();
                var objectid = claims.Where( x => x.Type.ToLower() == "http://schemas.microsoft.com/identity/claims/objectidentifier").Single().ClaimValue;
                //var objectid = "test";
                var accountName = configuration.GetSection("adlsgen2").GetSection("accountName").Value;

                var accountKey = getAccountKey("mediaforanalysisacckey");

                
                
                StorageSharedKeyCredential sharedKeyCredential =new StorageSharedKeyCredential(accountName, accountKey);


                string dfsUri = "https://" + accountName + ".dfs.core.windows.net";

                var dataLakeServiceClient = new DataLakeServiceClient(new Uri(dfsUri), sharedKeyCredential);
                var mediaFileSystem  = dataLakeServiceClient.GetFileSystemClient("mediafiles");
                
                DataLakeDirectoryClient directoryClient = mediaFileSystem.GetDirectoryClient(objectid);
                await directoryClient.CreateIfNotExistsAsync();


                var fileClient = directoryClient.GetFileClient( Guid.NewGuid().ToString() + MD.fileExtension);
                // convert string to stream
                byte[] byteArray =  Convert.FromBase64String(MD.Mediabase64);
            
                MemoryStream stream = new MemoryStream(byteArray);

            


                var upload1 = await fileClient.UploadAsync(stream, overwrite:true);
                

                AccountSasBuilder sas = new AccountSasBuilder
                {
                    Protocol = SasProtocol.None,
                    Services = AccountSasServices.Blobs,
                    ResourceTypes = AccountSasResourceTypes.All,
                    StartsOn = DateTimeOffset.UtcNow.AddHours(-1),
                    ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),

                };
                sas.SetPermissions(AccountSasPermissions.Read);
                UriBuilder sasUri = new UriBuilder(fileClient.Uri);
                sasUri.Query = sas.ToSasQueryParameters(sharedKeyCredential).ToString();
                DogBreedDetectRequest dreq = new  DogBreedDetectRequest();
                dreq.filelocations = new List<string>();
                dreq.filelocations.Add(sasUri.ToString());
                AnalysisResponse AR = new  AnalysisResponse();
                AR = await  Analyze(dreq);
                return (AR);
            }
            catch(Exception ex)
            {
                AnalysisResponse AR = new  AnalysisResponse();
                AR.fileUri = ex.Message;
                return AR;
            }
        }

        [HttpPost]
        [Route("api/AnalyzeMedia")]
        [AllowAnonymous]
        public async Task<AnalysisResponse> Analyze([FromBody] DogBreedDetectRequest dreq){

                var dogbreedapikey = getAccountKey("dogbreedkey");
                
                HttpClient hc = new  HttpClient();
                hc.DefaultRequestHeaders.Add("Authorization", string.Format("Bearer {0}",dogbreedapikey));

                
                var response = await hc.PostAsJsonAsync<DogBreedDetectRequest>("http://8f0bc1cb-2b66-4d46-81a8-a7c8e93ba646.uksouth.azurecontainer.io/score",dreq);
               
                var breedData = JsonSerializer.Deserialize<List<string>>(await response.Content.ReadAsStringAsync());
                var breedLabel = breedData.First();
                AnalysisResponse AR = new  AnalysisResponse();
                AR.fileUri = dreq.filelocations[0];
                AR.StuffToShow =  new List<singleAnalysisPoint>();
                singleAnalysisPoint itemToShow = new singleAnalysisPoint(){ Label="Breed", LabelValue=breedLabel};
                singleAnalysisPoint itemToShow1 = new singleAnalysisPoint(){ Label="Context", LabelValue="This model is trained using tranfer learning from inceptionv2 and with data from Kaggle"};
                AR.StuffToShow.Add(itemToShow1);
                AR.StuffToShow.Add(itemToShow);
                return AR;

        }
        

        private string getAccountKey(string secretName){
            SecretClientOptions options = new SecretClientOptions()
                            {
                                Retry =
                                {
                                    Delay= TimeSpan.FromSeconds(2),
                                    MaxDelay = TimeSpan.FromSeconds(16),
                                    
                                    MaxRetries = 5,
                                    Mode = RetryMode.Exponential
                                }
                            };
        var client = new SecretClient(new Uri("https://newmlappkeyvault.vault.azure.net/"), new DefaultAzureCredential(
                    new DefaultAzureCredentialOptions
                    {
                        ExcludeVisualStudioCredential = true,
                        ExcludeVisualStudioCodeCredential = true
                    }),options);

        KeyVaultSecret secret = client.GetSecret(secretName);

        string secretValue = secret.Value;
        

        return secretValue;
        }
    }
}
