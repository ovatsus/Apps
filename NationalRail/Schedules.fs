#if INTERACTIVE
#r "../lib/portable/FSharp.Data.dll"
#else
namespace NationalRail
#endif

open System
open System.Text
open FSharp.Net

type ScheduleType = 
    | Full
    | DailyUpdate of DayOfWeek
    member x.ToUrlPart1() = 
        match x with 
        | DailyUpdate _ -> "UPDATE"
        | Full -> "FULL"
    member x.ToUrlPart2() =
        match x with 
        | DailyUpdate day-> "toc-update-" + day.ToString().Substring(0,3).ToLowerInvariant()
        | Full -> "toc-full"

type Toc = 
    | All
    | ArrivaTrainsWales
    | C2C
    | CrossCountry
    | DevonAndCornwall
    | EastMidlandsTrains
    | Eurostar
    | FfestiniogRailway
    | FirstCapitalConnect
    | FirstGreatWestern
    | FirstHullTrains
    | FirstScotrail
    | FirstTranspennineExpress
    | GatwickExpress
    | GrandCentral
    | HeathrowConnect
    | HeathrowExpress
    | IslandLines
    | LondonMidland
    | LondonOverground
    | LULBakerlooLine
    | LULDistrictLineRichmond
    | LULDistrictLineWimbledon
    | Merseyrail
    | NationalExpressEastAnglia
    | NationalExpressEastCoast
    | Nexus
    | NorthYorkshireMoorsRailway
    | NorthernRail
    | Southeastern
    | Southern
    | StagecoachSouthWesternTrains
    | Chiltern
    | VirginWestCoast
    | WestCoastRailway
    | WrexhamAndShropshire
    member x.ToUrl() =
        match x with
        | All -> "ALL"        
        | ArrivaTrainsWales -> "HL"
        | C2C -> "HT"
        | CrossCountry -> "EH"
        | DevonAndCornwall -> "EN"
        | EastMidlandsTrains -> "EM"
        | Eurostar -> "GA"
        | FfestiniogRailway -> "XJ"
        | FirstCapitalConnect -> "EG"
        | FirstGreatWestern -> "EF"
        | FirstHullTrains -> "PF"
        | FirstScotrail -> "HA"
        | FirstTranspennineExpress -> "EA"
        | GatwickExpress -> "HV"
        | GrandCentral -> "EC"
        | HeathrowConnect -> "EE"
        | HeathrowExpress -> "HM"
        | IslandLines -> "HZ"
        | LondonMidland -> "EJ"
        | LondonOverground -> "EK"
        | LULBakerlooLine -> "XC"
        | LULDistrictLineRichmond -> "XE"
        | LULDistrictLineWimbledon -> "XB"
        | Merseyrail -> "HE"
        | NationalExpressEastAnglia -> "EB"
        | NationalExpressEastCoast -> "HB"
        | Nexus -> "PG"
        | NorthYorkshireMoorsRailway -> "PR"
        | NorthernRail -> "ED"
        | Southeastern -> "HU"
        | Southern -> "HW"
        | StagecoachSouthWesternTrains -> "HY"
        | Chiltern -> "HO"
        | VirginWestCoast -> "HF"
        | WestCoastRailway -> "PA"
        | WrexhamAndShropshire -> "EI"

module Schedules = 

    let download (username:string) (password:string) (toc:Toc) (schedule:ScheduleType) = 
        let url = sprintf "https://datafeeds.networkrail.co.uk/ntrod/CifFileAuthenticate?type=CIF_%s_%s_DAILY&day=%s" 
                          (toc.ToUrl()) 
                          (schedule.ToUrlPart1()) 
                          (schedule.ToUrlPart2())    
        let auth = "Basic " + (username + ":" + password |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)
        Http.RequestString(url, headers=["Authorization", auth])
