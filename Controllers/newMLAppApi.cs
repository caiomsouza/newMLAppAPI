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
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

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
                AnalysisResponse AR = new  AnalysisResponse();
                if(MD.analysisType == "dog"){
                    DogBreedDetectRequest dreq = new  DogBreedDetectRequest();
                    dreq.filelocations = new List<string>();
                    dreq.filelocations.Add(sasUri.ToString());
                    AR = await  Analyze(dreq);
                }
                if(MD.analysisType.ToLower() == "indianbread" || MD.analysisType.ToLower() == "chutney"){
                    CustomVisionAPIRequest CVR = new  CustomVisionAPIRequest();
                    CVR.url = sasUri.ToString();
                    AR = await CustomAnalyse(CVR, MD.analysisType);
                
                }
                 if(MD.analysisType.ToLower() == "cognitiveservice" ){
                    CustomVisionAPIRequest CVR = new  CustomVisionAPIRequest();
                    CVR.url = sasUri.ToString();
                    AR = await CogitiveServiceAnalysis(CVR);
                
                }                               
                
                return (AR);
            }
            catch(Exception ex)
            {
                AnalysisResponse AR = new  AnalysisResponse();
                AR.fileUri = ex.Message + ex.StackTrace;
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
        

        private static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
            new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
            { Endpoint = endpoint };
            return client;
        }

        [HttpPost]
        [Route("api/AnalyzeMediaCognitiveServices")]
        [AllowAnonymous]
        public async Task<AnalysisResponse> CogitiveServiceAnalysis([FromBody]CustomVisionAPIRequest CusReq){
            AnalysisResponse AR = new  AnalysisResponse();
            var cvkey = getAccountKey("computervisionkey");
            var cvClient = Authenticate("https://computervisionmsmlapp.cognitiveservices.azure.com/",cvkey);
              List<VisualFeatureTypes?> features = new List<VisualFeatureTypes?>()
                                                        {
                                                            VisualFeatureTypes.Categories, VisualFeatureTypes.Description,
                                                            VisualFeatureTypes.Faces, VisualFeatureTypes.ImageType,
                                                            VisualFeatureTypes.Tags, VisualFeatureTypes.Adult,
                                                            VisualFeatureTypes.Color, VisualFeatureTypes.Brands,
                                                            VisualFeatureTypes.Objects
                                                        };
            var response = await cvClient.AnalyzeImageAsync(CusReq.url,features);
            AR.fileUri = CusReq.url;
            AR.StuffToShow = new List<singleAnalysisPoint>();
            
            if(response.Faces != null){
                singleAnalysisPoint SAP1 = new singleAnalysisPoint();
                SAP1.Label = "Total Faces";
                SAP1.LabelValue = response.Faces.Count.ToString();
                AR.StuffToShow.Add(SAP1);
            }
            if(response.Adult != null){
                    singleAnalysisPoint SAP2 = new singleAnalysisPoint();
                    SAP2.Label = "Adult Score";
                    SAP2.LabelValue = (Math.Round(response.Adult.AdultScore,2)).ToString();
                    AR.StuffToShow.Add(SAP2);

            }
            if(response.Description != null){
                singleAnalysisPoint SAP3 = new singleAnalysisPoint();
                SAP3.Label = "Caption";
                SAP3.LabelValue = string.Format("{0} Confidence {1}",response.Description.Captions.OrderByDescending(a => a.Confidence).First().Text, (Math.Round(response.Description.Captions.OrderByDescending(a => a.Confidence).First().Confidence,2)).ToString());
                AR.StuffToShow.Add(SAP3);
            }

            if(response.Objects != null){
                foreach(var x in response.Objects){
                    singleAnalysisPoint SAP4 = new singleAnalysisPoint();
                    SAP4.Label =  "Object";
                    SAP4.LabelValue = string.Format("{0} Confidence {1}",x.ObjectProperty,(Math.Round(x.Confidence,2)).ToString());
                    AR.StuffToShow.Add(SAP4);

                }

            }
            if(response.Categories != null){
                foreach(var x in response.Categories){
                    singleAnalysisPoint SAP5 = new singleAnalysisPoint();
                    SAP5.Label = x.Name;
                    SAP5.LabelValue = (Math.Round(x.Score,2)).ToString();
                    AR.StuffToShow.Add(SAP5);
                }
            }





            return AR;

        }

        private async Task<AnalysisResponse> CustomAnalyse( CustomVisionAPIRequest CusReq, string mediaType){

                var apikey = getAccountKey("customvision" + mediaType);
                                   // customvisionchutney
                    //customvisionindianbread
                
                HttpClient hc = new  HttpClient();
                hc.DefaultRequestHeaders.Add("Prediction-Key", apikey);
                HttpResponseMessage response = new HttpResponseMessage();

                AnalysisResponse AR = new AnalysisResponse();
                AR.fileUri = CusReq.url;
                AR.StuffToShow = new List<singleAnalysisPoint>();
                if(mediaType == "chutney")
                     response = await hc.PostAsJsonAsync<CustomVisionAPIRequest>("https://uksouth.api.cognitive.microsoft.com/customvision/v3.0/Prediction/7ffefc23-20ae-4eed-8664-c5a578b526df/classify/iterations/Iteration2/url",CusReq);
                else
                    response = await hc.PostAsJsonAsync<CustomVisionAPIRequest>("https://uksouth.api.cognitive.microsoft.com/customvision/v3.0/Prediction/5517aa7f-402e-4a98-8706-c18901a9b568/classify/iterations/Iteration1/url",CusReq);
               
                var responsedata = JsonSerializer.Deserialize<CustomVisionResponse>(await response.Content.ReadAsStringAsync());

                singleAnalysisPoint contextitem = new singleAnalysisPoint(){ Label="Context", LabelValue="Azure CustomVision in action"};
                AR.StuffToShow.Add(contextitem);

                foreach(var x in responsedata.predictions){
                    singleAnalysisPoint itemToShow = new singleAnalysisPoint(){ Label="Class", LabelValue=string.Format("{0}, chance {1} Percent",x.tagName, Math.Round(x.probability,2))};
                    AR.StuffToShow.Add(itemToShow);
                };
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
