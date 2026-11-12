using AmenityExtractor.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmenityExtractor
{
    public static class GUIDAssigner
    {
        /*
         GUIDs are assigned the way the game client does it (verified against debugger dumps of the client for map04, map05, map13 and ugd04: every object gets the same GUID):

         1. All map objects go into ONE list in creation order (our Id order). The client then sorts that list with an STLport stable_sort (see ClientStableSort).
         2. The comparator (ClientObjectLess) puts objects that have a map id (structures and groundcover roots, i.e. !IsChildObject) before everything else, then compares the type name
            case-insensitively (_stricmp), then X and Y, which only count when they differ by at least 0.1 (otherwise the next axis decides), then Z.
            The tolerance makes the comparator inconsistent for near ties, so the exact stable_sort algorithm matters, not just the key.
         3. GUIDs are handed out one by one in that order. Some objects reserve extra GUIDs for sub-objects (see objectTypesWithMultipleGuids).
         4. The coordinates compared are the ones the client computes: the parent rotation is the exact 14-bit angle (not rounded to degrees) and .lst coordinates are parsed by truncating toward
            zero (ClientFloat). They are kept in SortX/SortY/SortZ; the exported AbsX/AbsY/AbsZ are unchanged.

         Some objects such as aircraft landing pads or repair silos have multiple GUIDs assigned: one per sub-terminal (rearm, vehicle recharge, BFR rearm), allocated from the same pool.
         Objects the client creates but we do not export (unused LLU sockets) still consume a GUID, which shows up as a gap.
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


            // Cyssor has a gap between the monolith object (GUID 59 clientside) and the first tech_plant (Wele - GUID 66 clientside) object that is not explained yet (map03 has not been captured from the client).
            // Until it is, the workaround below skips 6 GUIDs ahead after the monolith.
            objectNamesWithMultipleGuids.Add("monolith_cyssor", 6);

            // Repair silos in sanctuaries are groundcover roots (not part of a building) and are numbered in sort order like any other root; no special skip is needed
            // (confirmed on the VS sanctuary, map13).

            // Sort like the client (see the comment at the top of this class), then hand out GUIDs in that order.
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
