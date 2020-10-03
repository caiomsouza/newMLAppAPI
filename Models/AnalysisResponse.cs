using System.Collections.Generic;
using System;
public class AnalysisResponse {
    public string fileUri {get;set;}
    public List<singleAnalysisPoint> StuffToShow{get;set;}

}

public class singleAnalysisPoint{
    public string Label {get;set;}
    public string LabelValue {get;set;}
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class CustomVisionPrediction    {
        public double probability { get; set; } 
        public string tagId { get; set; } 
        public string tagName { get; set; } 
    }

    public class CustomVisionResponse    {
        public string id { get; set; } 
        public string project { get; set; } 
        public string iteration { get; set; } 
        public DateTime created { get; set; } 
        public List<CustomVisionPrediction> predictions { get; set; } 
    }
