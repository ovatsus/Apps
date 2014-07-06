#r "../packages/FSharp.Data.2.0.9/lib/portable-net40+sl5+wp8+win8/FSharp.Data.dll"
#r "../packages/HtmlAgilityPack-PCL.1.4.6/lib/HtmlAgilityPack-PCL.dll"

open System
open System.IO
open FSharp.Data

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

type Topics = JsonProvider<"topics.json", SampleIsList=true, RootName="topic">

let topics = 
    File.ReadAllText("topics.json")
    |> JsonValue.ParseMultiple
    |> Seq.collect (fun x -> x.AsArray())
    |> Seq.map (fun x -> new Topics.Topic(x))

for topic in topics do
    for course in topic.Courses do
        printfn "%A" (topic.Visibility, topic.Display, course.Active, course.Status, course.CertificatesReady, topic.Name)
