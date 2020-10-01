using System.Collections.Generic;
public class DogBreedDetectRequest{

    public List<string> filelocations {get;set;}
}

public class DogBreedDetectResponse{
    public Dictionary<string,string> Breed{get;set;}

}