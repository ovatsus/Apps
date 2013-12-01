#r "../lib/portable/FSharp.Data.dll"
#r "../packages/HtmlAgilityPack-PCL.1.4.6/lib/HtmlAgilityPack-PCL.dll"

open System
open System.IO
open FSharp.Data

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

type Topics = JsonProvider<"topics.json", SampleIsList=true, RootName="topic">

let topics = Topics.Parse(File.ReadLines("topics.json") |> Seq.last)

for topic in topics do
    for course in topic.Courses do
        printfn "%A" (topic.Visibility.JsonValue.ToString(), topic.Display, course.Active, course.Status, course.CertificatesReady, topic.Name)
