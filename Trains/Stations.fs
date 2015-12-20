namespace Trains

open System.Reflection
open FSharp.Control
open FSharp.Data
open FSharp.GeoUtils

type Country = 
    | UK
    | Ireland
    member x.SupportsArrivals =
        match x with
        | UK -> true
        | Ireland -> true

type Station =
    { Code : string
      Name : string
      Location : LatLong }
    override x.ToString() = x.Name

module Stations = 

    let mutable Country = UK

    let private stationInfo = ref None

    let private getStationInfo() = 

        match !stationInfo with
        | None ->

            let allStations =

                match Country with
                | UK ->

                    //from RailReferences.csv in http://www.data.gov.uk/dataset/naptan
                    //XY location (columns 7/8) to latitude/longitude converted with http://gridreferencefinder.com/batchConvert/batchConvert.htm
#if INTERACTIVE
                    let csvFile = new CsvProvider<"UKStations.csv", Schema="Latitude=float,Longitude=float">()
#else
                    use stream = Resources.getResourceStream "UKStations.csv" "Trains"
                    let csvFile = CsvProvider<"UKStations.csv", Schema="Latitude=float,Longitude=float">.Load stream
#endif
                    csvFile.Rows
                    |> Seq.groupBy (fun station -> station.CrsCode)
                    |> Seq.map (fun (code, stations) -> 
                        let station = 
                            stations 
                            |> Seq.sortBy (fun station -> -station.RevisionNumber) 
                            |> Seq.head
                        { Code = code
                          Name = station.StationName |> String.remove " Rail Station" |> String.remove " Railway Station"
                          Location = LatLong.Create station.Latitude station.Longitude })
                    |> Seq.toList


                | Ireland ->

                    //from http://api.irishrail.ie/realtime/realtime.asmx/getAllStationsXML
#if INTERACTIVE
                    let xml = XmlProvider<"IrelandStations.xml">.GetSample()
#else
                    use stream = Resources.getResourceStream "IrelandStations.xml" "Trains"
                    let xml = XmlProvider<"IrelandStations.xml">.Load stream
#endif
                    xml.ObjStations
                    |> Seq.distinctBy (fun station -> station.StationDesc) // there's two different Adamstown
                    |> Seq.map (fun station -> 
                        { Code = station.StationCode.Trim()
                          Name = station.StationDesc.Trim()
                          Location = LatLong.Create (float station.StationLatitude) (float station.StationLongitude) })
                    |> Seq.toList

            let stationsByCode =
                allStations 
                |> List.map (fun station -> station.Code, station) 
                |> dict 

            stationInfo := Some (allStations, stationsByCode)
            allStations, stationsByCode

        | Some stationInfo -> stationInfo

    [<CompiledName("GetNearest")>]
    let getNearest currentPosition limit = 

        let allStations, _ = getStationInfo()

        let nearestStations =
            allStations
            |> List.map (fun station -> station.Location - currentPosition, station)
            |> List.filter (fun (dist, _) -> dist < 9.5)
            |> List.sortBy (fun (dist, station) -> dist, station.Name)
            |> Seq.truncate limit
            |> Seq.toArray

        nearestStations

    [<CompiledName("GetNearestAsync")>]
    let getNearestAsync currentPosition limit = 
        async { 
            return getNearest currentPosition limit
        } |> LazyAsync.fromAsync

    [<CompiledName("GetAll")>]
    let getAll() =     
        let allStations, _ = getStationInfo()
        allStations
        |> List.sortBy (fun station -> station.Name)
        |> Seq.toArray

    [<CompiledName("Get")>]
    let get stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.[stationCode]

    [<CompiledName("TryGet")>]
    let tryGet stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.TryGetValue stationCode
