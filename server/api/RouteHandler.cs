﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace Maps.API
{
    public class RouteHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        protected override string ServiceName { get { return "route"; } }

        private World ResolveLocation(HttpContext context, string field, ResourceManager manager, SectorMap map)
        {
            string query = context.Request.QueryString[field];
            if (String.IsNullOrWhiteSpace(query))
            {
                SendError(context.Response, 400, "Bad Request", String.Format("Missing {0} location", field));
                return null;
            }

            query = query.Trim();

            Match match = Regex.Match(query, @"^(?<sector>.+?)\s+(?<hex>\d\d\d\d)$");
            if (!match.Success)
            {
                int x = GetIntOption(context, "x", 0);
                int y = GetIntOption(context, "y", 0);
                WorldLocation loc = SearchEngine.FindNearestWorldMatch(query, x, y);
                if (loc == null)
                {
                    SendError(context.Response, 404, "Not Found", String.Format("Location not found: {0}", query));
                    return null;
                }
                Sector loc_sector;
                World loc_world;
                loc.Resolve(map, manager, out loc_sector, out loc_world);
                return loc_world;
            } 

            Sector sector = map.FromName(match.Groups["sector"].Value);
            if (sector == null)
            {
                SendError(context.Response, 404, "Not Found", String.Format("Sector not found: {0}", sector));
                return null;
            }

            string hexString = match.Groups["hex"].Value;
            int hex = Int32.Parse(hexString);
            int hx = hex / 100, hy = hex % 100;
            if (!Util.InRange(hx, 1, Astrometrics.SectorWidth) || !Util.InRange(hy, 1, Astrometrics.SectorHeight))
            {
                SendError(context.Response, 400, "Not Found", String.Format("Invalid hex: {0}", hexString));
                return null;
            }

            World world = sector.GetWorlds(manager)[hex];
            if (world == null)
            {
                SendError(context.Response, 404, "Not Found", String.Format("No such world: {0} {1}", sector.Names[0].Text, hexString));
                return null;
            }

            return world;
        }

        public override void Process(HttpContext context)
        {
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            World startWorld = ResolveLocation(context, "start", resourceManager, map);
            if (startWorld == null)
                return;

            World endWorld = ResolveLocation(context, "end", resourceManager, map);
            if (endWorld == null)
                return;

            int jump = Util.Clamp(GetIntOption(context, "jump", 2), 0, 12);

            var finder = new TravellerPathFinder(resourceManager, map, jump);
            List<World> route = finder.FindPath(startWorld, endWorld);
            if (route == null) { SendError(context.Response, 404, "Not Found", "No route found"); return; }

            List<RouteStop> result = new List<RouteStop>(
                route.Select(w => new RouteStop(w)));
            SendResult(context, result);
        }

        public class RouteStop
        {
            public RouteStop() { }
            public RouteStop(World w)
            {
                Sector = w.SectorName;
                SectorX = w.Sector.X;
                SectorY = w.Sector.Y;

                Subsector = w.SubsectorName;

                Name = w.Name;
                Hex = w.Hex;
                HexX = w.X;
                HexY = w.Y;
            }

            public string Sector { get; set; }
            public int SectorX { get; set; }
            public int SectorY { get; set; }

            public string Subsector { get; set; }

            public string Name { get; set; }
            public string Hex { get; set; }
            public int HexX { get; set; }
            public int HexY { get; set; }
        }
    }
}