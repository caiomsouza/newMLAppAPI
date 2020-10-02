using System.Collections.Generic;
public class AnalysisResponse {
    public string fileUri {get;set;}
    public List<singleAnalysisPoint> StuffToShow{get;set;}

}

public class singleAnalysisPoint{
    public string Label {get;set;}
    public string LabelValue {get;set;}
}
