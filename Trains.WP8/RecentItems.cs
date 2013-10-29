using System;
using System.Collections.Generic;
using System.Linq;
using Common.WP8;

namespace Trains.WP8
{
    public static class RecentItems
    {
        private static readonly List<DeparturesAndArrivalsTable> allRecentItems =
            Settings.GetString(Setting.RecentStations)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(DeparturesAndArrivalsTable.Parse)
                .Where(x => x != null)
                .ToList();

        public static List<DeparturesAndArrivalsTable> GetItemsToDisplay(Station fromStation, string excludeStation) 
        {
            var recentItemsToDisplay = fromStation == null ? allRecentItems.ToList() :
                (from item in allRecentItems
                 let target = item.HasDestinationFilter && item.Station.Code == fromStation.Code && item.CallingAt.Value.Code != excludeStation ? item.CallingAt.Value :
                              item.Station.Code != excludeStation && item.Station.Code != fromStation.Code ? item.Station :
                              null
                 where target != null
                 select DeparturesAndArrivalsTable.Create(target)).Distinct().ToList();

            return recentItemsToDisplay;
        }

        public static void Add(DeparturesAndArrivalsTable recentItem)
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

        public static void Remove(DeparturesAndArrivalsTable recentItem)
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
