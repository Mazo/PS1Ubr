using AmenityExtractor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmenityExtractor
{
    public static class GUIDAssigner
    {
        /*
         GUIDs are assigned in a very specific order, and certain objects have priority over others, for example structures are assigned GUIDs first

         Some objects such as aircraft landing pads or repair silos have multiple GUIDs assigned. This is likely one GUID per function (e.g. one GUID for the rearm, one or more for repair / AT recharge. Exact details are unknown currently)

         GUIDs are typically assigned in the order of Object Type -> X Position -> Y Position -> Z Position
        */

        public static void AssignGUIDs(List<PlanetSideObject> mapObjects)
        {
            var currentGUID = 1;

            // Some objects reserve extra guids for unknown reasons currently, so we need to skip ahead if one of those is found
            // A good example of this that was previously unknown is the repair_silo object. It has three extra GUIDs assigned after it for terminals: which are for BFR Rearming, Ground vehicle rearming and ground vehicle repair
            // Adding these objects to the map is currently hardcoded within the PSF-MapGenerator project
            var objectTypesWithMultipleGuids = new Dictionary<string, int>()
            {
                { "amp_station", 2 }, { "comm_station", 2 }, {"comm_station_dsp", 2}, {"cryo_facility", 2 }, {"tech_plant", 2}, {"pad_landing_frame", 2},
                { "pad_landing_tower_frame", 2}, {"repair_silo", 3}, {"crystals_vehicle_a", 1}, {"crystals_vehicle_b", 1}
            };

            var objectNamesWithMultipleGuids = new Dictionary<string, int>();


            // For some unknown reason Cyssor is the only map with a gap between the monolith object (GUID 59 clientside) and the first tech_plant (Wele - GUID 66 clientside) object.
            // So far efforts to determine why have proved fruitless, so the below is a workaround to skip 6 GUIDs ahead after the monolith.
            objectNamesWithMultipleGuids.Add("monolith_cyssor", 6);

            //Repair silos in sanctuaries aren't assigned to a building and are added in the groundcover file. Currently this will break the GUID assignments as a repair_silo is suddenly a structure in it's own right
            // For now, skip them by jumping ahead after the last HART building in each Sanctuary.
            //objectNamesWithMultipleGuids.Add("Esamir_HART", 24); // VS - 6 repair silos
            //objectNamesWithMultipleGuids.Add("Hart_Forseral", 48); // TR - 12 repair silos
            //objectNamesWithMultipleGuids.Add("Hart_Hossin", 24); // NC - 6 repair silos

            // The client sorts every map object in one vector (STLport stable_sort over the objects in creation order): structures first (objects with a map id), then everything else, each group ordered by
            // name (case-insensitive), then X and Y with a 0.1 tolerance, then Z. GUIDs are handed out in that order. See ClientStableSort and ClientObjectLess.
            var orderedObjects = ClientStableSort.Sort(mapObjects.OrderBy(x => x.Id).ToList(), ClientObjectLess);

            foreach (var obj in orderedObjects)
            {
                obj.GUID = currentGUID;
                currentGUID++;
                if (objectTypesWithMultipleGuids.ContainsKey(obj.ObjectType)) currentGUID += objectTypesWithMultipleGuids[obj.ObjectType]; // Skip reserved GUIDS
                if (objectNamesWithMultipleGuids.ContainsKey(obj.ObjectName)) currentGUID += objectNamesWithMultipleGuids[obj.ObjectName]; // Skip reserved GUIDS

                // ! prefix found in cave groundcover files indicate the object should be classed as an "entity" guid, not a structure guid. Trim it once processed.
                obj.ObjectType = obj.ObjectType.Replace("!", "");
                obj.ObjectName = obj.ObjectName.Replace("!", "");
            }
        }

        private const float ClientCoordinateTolerance = 0.1f;

        // Comparator from the game client (a < b): objects with a map id (non-children) first, then _stricmp on the name (A-Z folded to lowercase), then X and Y only compared when they differ by at least 0.1, otherwise Z.
        private static bool ClientObjectLess(PlanetSideObject a, PlanetSideObject b)
        {
            if (a.IsChildObject != b.IsChildObject) return !a.IsChildObject;
            var c = CompareNamesIgnoreCase(a.ObjectType.Replace("!", ""), b.ObjectType.Replace("!", ""));
            if (c != 0) return c < 0;
            if (Math.Abs((float)a.SortX - (float)b.SortX) >= ClientCoordinateTolerance) return (float)a.SortX < (float)b.SortX;
            if (Math.Abs((float)a.SortY - (float)b.SortY) >= ClientCoordinateTolerance) return (float)a.SortY < (float)b.SortY;
            return (float)a.SortZ < (float)b.SortZ;
        }

        private static int CompareNamesIgnoreCase(string a, string b)
        {
            var length = Math.Min(a.Length, b.Length);
            for (var i = 0; i < length; i++)
            {
                var ca = char.ToLowerInvariant(a[i]);
                var cb = char.ToLowerInvariant(b[i]);
                if (ca != cb) return ca < cb ? -1 : 1;
            }
            return a.Length.CompareTo(b.Length);
        }
    }
}
