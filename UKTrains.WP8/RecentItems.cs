using System;
using System.Collections.Generic;
using System.Linq;
using Trains;

namespace UKTrains
{
    public static class RecentItems
    {
        private static readonly List<DeparturesTable> allRecentItems =
            Settings.GetString(Setting.RecentStations)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(DeparturesTable.Parse)
                .ToList();

        public static List<DeparturesTable> GetItemsToDisplay(Station fromStation, string excludeStation) 
        {
            var recentItemsToDisplay = fromStation == null ? allRecentItems.ToList() :
                (from item in allRecentItems
                 let target = item.HasDestinationFilter && item.Station.Code == fromStation.Code && item.CallingAt.Value.Code != excludeStation ? item.CallingAt.Value :
                              item.Station.Code != excludeStation && item.Station.Code != fromStation.Code ? item.Station :
                              null
                 where target != null
                 select DeparturesTable.Create(target)).Distinct().ToList();

            return recentItemsToDisplay;
        }

        public static void Add(DeparturesTable recentItem)
        {
            if (recentItem.HasDestinationFilter &&
                allRecentItems.Count > 0 &&
                !allRecentItems[0].HasDestinationFilter &&
                allRecentItems[0].Station.Code == recentItem.Station.Code)
            {
                allRecentItems.RemoveAt(0);
            }
            allRecentItems.Remove(recentItem);
            allRecentItems.Insert(0, recentItem);
            Save();
        }

        public static void Remove(DeparturesTable recentItem)
        {
            allRecentItems.Remove(recentItem);
            Save();
        }

        public static void Clear()
        {
            allRecentItems.Clear();
            Save();
        }

        private static void Save()
        {
            Settings.Set(Setting.RecentStations, string.Join(",", allRecentItems.Select(item => item.Serialize())));
        }

    }
}
