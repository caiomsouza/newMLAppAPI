using System.Collections.Generic;
public class DogBreedDetectRequest{

    public List<string> filelocations {get;set;}
}
public class CustomVisionAPIRequest{
    public string url {get;set;}
}

public class CustomVisionWrapper  : CustomVisionAPIRequest{
    public string mediaType {get;set;}

}

public class nlpRequest {
    public List<string> sentences {get;set;}
}