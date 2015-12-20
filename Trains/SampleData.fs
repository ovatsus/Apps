﻿namespace Trains

open System
open System.IO
open System.Reflection
open FSharp.Control

type SampleData() =
    inherit Common.SampleData()

    let stations = Stations.getAll() |> Array.sortBy (fun s -> s.Name.Length)
    
    let s1 = stations.[stations.Length / 2]
    let s2 = stations.[0]
    let s3 = stations.[stations.Length - 1]
    let s4 = stations.[2 * stations.Length / 3]

    let t1 = Time.Create( 0,  9)
    let t2 = Time.Create(23, 59)
    let t3 = Time.Create(16, 47)
    let t4 = Time.Create(20,  8)

    let details = LazyAsync.fromValue [| |]

    let departures1 = 
        use stream = Resources.getResourceStream "UKDepartures.html" "Trains"
        use streamReader = new StreamReader(stream)
        let html = streamReader.ReadToEnd()
        LiveDepartures.UK.getDeparturesFromHtml html None null Unchecked.defaultof<_>

    let departures2 = 
        [ { Due = t1
            Destination = s1.Name
            DestinationDetail = "via " + s2.Name
            Status = Status.Cancelled
            Platform = Some "22C"
            Details = details
            Arrival = ref None
            PropertyChangedEvent = Event<_,_>().Publish }
          { Due = t2
            Destination = s2.Name
            DestinationDetail = ""
            Status = Status.OnTime
            Platform = None
            Details = details
            Arrival = ref <| Some { Due = Some t3
                                    Destination = s3.Name
                                    Status = Status.Delayed 4 }
            PropertyChangedEvent = Event<_,_>().Publish }
          { Due = t3
            Destination = s3.Name
            DestinationDetail = "via " + s1.Name + " & " + s2.Name + " via " + s4.Name
            Status = Status.Delayed 130
            Platform = Some "4"
            Details = details
            Arrival = ref None
            PropertyChangedEvent = Event<_,_>().Publish }
          { Due = t4
            Destination = s4.Name
            DestinationDetail = ""
            Status = Status.DelayedIndefinitely
            Platform = None
            Details = details
            Arrival = ref <| Some { Due = Some t1
                                    Destination = s1.Name
                                    Status = Status.OnTime }
            PropertyChangedEvent = Event<_,_>().Publish }
          { Due = t1
            Destination = s1.Name
            DestinationDetail = "via " + s3.Name
            Status = Status.Cancelled
            Platform = Some "12"
            Details = details
            Arrival = ref None
            PropertyChangedEvent = Event<_,_>().Publish }
          { Due = t2
            Destination = s2.Name
            DestinationDetail = ""
            Status = Status.OnTime
            Platform = None
            Details = details
            Arrival = ref None
            PropertyChangedEvent = Event<_,_>().Publish } ]

    let liveProgress1 = 
        use stream = Resources.getResourceStream "UKLiveProgress.html" "Trains"
        use streamReader = new StreamReader(stream)
        let html = streamReader.ReadToEnd()
        LiveDepartures.UK.getJourneyDetailsFromHtml None (Time.Create(0)) html

    let liveProgress2 = 
        [ { Arrives = Some t1
            Station = s1.Name
            Status = OnTime true
            Platform = None
            IsAlternateRoute = false }
          { Arrives = Some t2
            Station = s2.Name
            Status = Delayed (true, 5)
            Platform = Some "6"
            IsAlternateRoute = false }
          { Arrives = Some t3
            Station = s3.Name
            Status = NoReport
            Platform = None
            IsAlternateRoute = false }
          { Arrives = Some t4
            Station = s4.Name
            Status = Cancelled
            Platform = Some "20D"
            IsAlternateRoute = false }
          { Arrives = Some t1
            Station = "* " + s1.Name
            Status = OnTime false
            Platform = None
            IsAlternateRoute = true }
          { Arrives = Some t1
            Station = "* " + s2.Name
            Status = Delayed (false, 5)
            Platform = Some "21"
            IsAlternateRoute = true } ]

    member __.NearestStations = [ 1.1, s1 
                                  3.4, s2 
                                  5.6, s3 
                                  7.8, s4
                                  9.2, s1 ]

    member __.RecentStations = [ DeparturesAndArrivalsTable.Create(s1)
                                 DeparturesAndArrivalsTable.Create(s2, s3)
                                 DeparturesAndArrivalsTable.Create(s4)
                                 DeparturesAndArrivalsTable.Create(s3, s1) ]
    
    member x.StationTitle = x.RecentStations.[2]

    member __.AllStations = [ s1; s2; s3; s4 ]
    
    member __.Departures = departures2

    member x.Arrivals = [ { Due = t1
                            Origin = s1.Name
                            Status = Status.Cancelled
                            Platform = Some "22C"
                            Details = details }
                          { Due = t2
                            Origin = s2.Name
                            Status = Status.OnTime
                            Platform = None
                            Details = details }
                          { Due = t3
                            Origin = s3.Name
                            Status = Status.Delayed 1
                            Platform = Some "4"
                            Details = details }
                          { Due = t4
                            Origin = s4.Name
                            Status = Status.Delayed 130
                            Platform = None
                            Details = details }
                          { Due = t1
                            Origin = s1.Name
                            Status = Status.DelayedIndefinitely
                            Platform = Some "12"
                            Details = details }
                          { Due = t2
                            Origin = s2.Name
                            Status = Status.OnTime
                            Platform = None
                            Details = details } ]

    member __.LiveProgress = liveProgress2

    member __.LastUpdated = "last updated at " + DateTime.Now.ToString("HH:mm:ss")
