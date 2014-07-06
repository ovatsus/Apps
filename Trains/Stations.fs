namespace Trains

open System
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

                    //from http://www.data.gov.uk/dataset/naptan
                    //osgb36 to latitude/longitude converted with http://gridreferencefinder.com/batchConvert/batchConvert.htm
#if INTERACTIVE
                    let csvFile = new CsvProvider<"UKStations.csv", Schema="Latitude=float,Longitude=float">()
#else
                    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UKStations.csv")
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
                          Name = station.StationName |> remove " Rail Station" |> remove " Railway Station"
                          Location = LatLong.Create station.Latitude station.Longitude })
                    |> Seq.toList


                | Ireland ->

                    //from "http://api.irishrail.ie/realtime/realtime.asmx/getAllStationsXML
#if INTERACTIVE
                    let xml = XmlProvider<"IrelandStations.xml">.GetSample()
#else
                    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("IrelandStations.xml")
                    let xml = XmlProvider<"IrelandStations.xml">.Load stream
#endif
                    xml.ObjStations
                    |> Seq.distinctBy (fun station -> station.StationDesc) // there's two different Adamstown
                    |> Seq.map (fun station -> 
                        { Code = station.StationCode
                          Name = station.StationDesc
                          Location = LatLong.Create (float station.StationLatitude) (float station.StationLongitude) })
                    |> Seq.toList

//#if INTERACTIVE
//                    let csvFile = new CsvProvider<"IrelandStations.csv">()
//#else
//                    use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("IrelandStations.csv")
//                    let csvFile = CsvProvider<"IrelandStations.csv">.Load stream
//#endif
//                    csvFile.Data 
//                    |> Seq.filter (fun station -> station.Code <> "")
//                    |> Seq.map (fun station -> 
//                        { Code = station.Code
//                          Name = station.Name
//                          Location = LatLong.Create station.Latitude station.Longitude })
//                    |> Seq.toList

            let stationsByCode =
                allStations 
                |> Seq.map (fun station -> station.Code, station) 
                |> dict 

            stationInfo := Some (allStations, stationsByCode)
            allStations, stationsByCode

        | Some stationInfo -> stationInfo

    [<CompiledName("GetNearest")>]
    let getNearest currentPosition limit = 

        let allStations, _ = getStationInfo()

        let nearestStations =
            allStations
            |> Seq.map (fun station -> station.Location - currentPosition, station)
            |> Seq.filter (fun (dist, _) -> dist < 9.5)
            |> Seq.sortBy (fun (dist, station) -> dist, station.Name)
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
        |> Seq.sortBy (fun station -> station.Name)
        |> Seq.toArray

    [<CompiledName("Get")>]
    let get stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.[stationCode]

    [<CompiledName("TryGet")>]
    let tryGet stationCode =
        let _, stationsByCode = getStationInfo()
        stationsByCode.TryGetValue stationCode
