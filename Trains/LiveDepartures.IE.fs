module Trains.LiveDepartures.IE

open FSharp.Control
open FSharp.Data
open FSharp.Net
open Trains

type XmlT = XmlProvider<"http://api.irishrail.ie/realtime/realtime.asmx/getStationDataByCodeXML?StationCode=mhide">

let xmlToDeparture (xml:XmlT.DomainTypes.ObjStationData) =
    let arrivalInformation = 
        { Due = Time.Create xml.Destinationtime
          Destination = xml.Destination
          Status = Status.OnTime } 
    { Due = Time.Create xml.Schdepart
      Destination = xml.Destination
      DestinationDetail = ""
      Status = Status.OnTime
      Platform = None
      Details = LazyAsync.fromValue [| |]
      Arrival = ref <| Some arrivalInformation
      PropertyChangedEvent = Event<_,_>().Publish }

let getDepartures (departureType:DepartureType) (departuresTable:DeparturesTable) = 

    assert (departureType = Departure)

    async {

        let url = "http://api.irishrail.ie/realtime/realtime.asmx/getStationDataByCodeXML?StationCode=" + departuresTable.Station.Code
        let! xml = Http.AsyncRequestString url
        let xmlT = XmlT.Parse xml

        let getDepartures() = 
            try 
                xmlT.GetObjStationDatas()
                |> Seq.map xmlToDeparture
                |> Seq.toArray
            with 
            | exn -> raise <| ParseError(sprintf "Failed to parse departures xml from %s:\n%s" url xml, exn)

        return getDepartures() 

    } |> LazyAsync.fromAsync
