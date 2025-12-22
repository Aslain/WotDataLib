using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RT.Util.ExtensionMethods;
using RT.Util.Json;

namespace WotDataLib
{
    public class WdData
    {
        public IDictionary<string, WdCountry> Countries { get; set; }
        public IList<string> Warnings { get; set; }

        public IEnumerable<WdTank> Tanks { get { return Countries.Values.SelectMany(c => c.Tanks.Values); } }

        internal GameInstallation Installation;
        internal GameVersionConfig VersionConfig;

        public WdData(GameInstallation installation, GameVersionConfig versionConfig)
        {
            Countries = new Dictionary<string, WdCountry>();
            Warnings = new List<string>();
            Installation = installation;
            VersionConfig = versionConfig;

            IList<string> countries = new[] { "ussr", "germany", "usa", "france", "china", "uk", "japan", "czech", "sweden", "poland", "italy", "intunion" };
            countries =
                countries.Where(
                    c =>
                        WotFileExporter.Exists(WotFileExporter.CombinePaths(installation.Path,
                            versionConfig.PathVehicleList.Replace(@"""Country""", c)))).ToList();

            string scriptsFolder = string.IsNullOrEmpty(versionConfig.PathSourceScripts) ? @"res\scripts" : versionConfig.PathSourceScripts;

			foreach (var country in countries)
			{
				JsonDict tanks, engines, guns, radios, shells;
				string path;
				System.Diagnostics.Debug.WriteLine($"=== DEBUG: Processing country: {country} ===");

				path = WotFileExporter.CombinePaths(installation.Path, versionConfig.PathVehicleList.Replace(@"""Country""", country));
				try
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: Reading tanks from: {path}");
					
					using (var stream = WotFileExporter.GetFileStream(path))
					{
						tanks = BxmlReader.ReadFile(stream);
						System.Diagnostics.Debug.WriteLine($"DEBUG: Tanks result type: {tanks?.GetType().Name}");
					}
				}
                catch (Exception e) { throw new WotDataException("Couldn't read vehicle list for country \"{0}\" from file \"{1}\"".Fmt(country, path), e); }

                path = WotFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\engines.xml").Fmt(country);
                try
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: Reading engines from: {path}");
                    using (var stream = WotFileExporter.GetFileStream(path))
                    {
                        engines = BxmlReader.ReadFile(stream);
						System.Diagnostics.Debug.WriteLine($"DEBUG: Engines result type: {engines?.GetType().Name}");
                    }
                }
                catch (Exception e) { throw new WotDataException("Couldn't read engines data for country \"{0}\" from file \"{1}\"".Fmt(country, path), e); }

                path = WotFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\guns.xml").Fmt(country);
                try
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: Reading guns from: {path}");
                    using (var stream = WotFileExporter.GetFileStream(path))
                    {
                        guns = BxmlReader.ReadFile(stream);
						System.Diagnostics.Debug.WriteLine($"DEBUG: Guns result type: {guns?.GetType().Name}");
                    }
                }
                catch (Exception e) { throw new WotDataException("Couldn't read guns data for country \"{0}\" from file \"{1}\"".Fmt(country, path), e); }

                path = WotFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\radios.xml").Fmt(country);
                try
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: Reading radios from: {path}");
                    using (var stream = WotFileExporter.GetFileStream(path))
                    {
                        radios = BxmlReader.ReadFile(stream);
						System.Diagnostics.Debug.WriteLine($"DEBUG: Radios result type: {radios?.GetType().Name}");
                    }
                }
                catch (Exception e) { throw new WotDataException("Couldn't read radios data for country \"{0}\" from file \"{1}\"".Fmt(country, path), e); }

                path = WotFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\shells.xml").Fmt(country);
                try
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: Reading shells from: {path}");
                    using (var stream = WotFileExporter.GetFileStream(path))
                    {
                        shells = BxmlReader.ReadFile(stream);
						System.Diagnostics.Debug.WriteLine($"DEBUG: Shells result type: {shells?.GetType().Name}");
                    }
                }
                catch (Exception e) { throw new WotDataException("Couldn't read shells data for country \"{0}\" from file \"{1}\"".Fmt(country, path), e); }

                // Nothing interesting in these:
                //chassis = BxmlReader.ReadFile(ZipFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\chassis.xml").Fmt(country));
                //turrets = BxmlReader.ReadFile(ZipFileExporter.CombinePaths(installation.Path, scriptsFolder, @"item_defs\vehicles\{0}\components\turrets.xml").Fmt(country));
                // Observe that these are the exact same pieces of information that are available directly in the vehicle definition (parsed in WdTank)

                try
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: Creating WdCountry for {country}");
                    Countries.Add(country, new WdCountry(country, this, tanks, engines, guns, radios, shells));
					System.Diagnostics.Debug.WriteLine($"DEBUG: Successfully added country {country}");
                }
                catch (Exception e)
                {
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR in WdCountry constructor: {e.Message}");
                    throw new WotDataException("Could not parse game data for country \"{0}\"".Fmt(country), e);
                }
            }

			foreach (var country in Countries.Values)
			{
                // Link all the modules to each tank
				foreach (var tank in country.Tanks.Values)
				{
					foreach (var key in tank.RawExtra["chassis"].GetDict().Keys)
						if (country.Chassis.TryGetValue(key, out var chassis))
							tank.Chassis.Add(chassis);
					foreach (var key in tank.RawExtra["turrets0"].GetDict().Keys)
						if (country.Turrets.TryGetValue(key, out var turret))
							tank.Turrets.Add(turret);
					foreach (var key in tank.RawExtra["engines"].GetDict().Keys)
						if (country.Engines.TryGetValue(key, out var engine))
							tank.Engines.Add(engine);
					foreach (var key in tank.RawExtra["radios"].GetDict().Keys)
						if (key != "" && country.Radios.TryGetValue(key, out var radio))
							tank.Radios.Add(radio);
				}
                // Guns are a bit weird; it appears that there's a base definition + turret-specific overrides.
				foreach (var turret in country.Turrets.Values)
					foreach (var kvp in turret.Raw["guns"].GetDict())
					{
						if (!country.Guns.ContainsKey(kvp.Key))
						{
							Warnings.Add("Could not complete gun loading for turret \"{0}\", gun \"{1}\"".Fmt(turret.Id, kvp.Key));
							continue;
						}
						var gun = country.Guns[kvp.Key].Clone();
						gun.UpdateFrom(kvp.Value.GetDict(), country);
						if (turret.Raw.ContainsKey("yawLimits"))  // earlier game versions have this data in the turret record
						{
							var parts = turret.Raw["yawLimits"].WdString().Split(' ').Select(x => decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
							gun.YawLeftLimit = parts[0]; // not too sure about which is which
							gun.YawRightLimit = parts[1];
						}
						turret.Guns.Add(gun);
					}
                // Validate that the guns loaded fully
				foreach (var gun in country.Guns.Values)
					try { gun.Validate(); }
					catch (Exception e) { Warnings.Add("Incomplete data for gun \"{0}\": {1}".Fmt(gun.Id, e.Message)); }
			}

            // Clear the string data, since it's no longer needed
            _moFiles.Clear();
            _moFiles = null;
        }

        private Dictionary<string, IDictionary<string, string>> _moFiles = new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        internal string ResolveString(string stringRef)
        {
            if (!stringRef.StartsWith("#"))
                return stringRef;
            var split = stringRef.Split(':');
            if (split.Length != 2) throw new WotDataException("Invalid string ref: " + stringRef);
            if (!split[0].StartsWith("#")) throw new WotDataException("Invalid string ref: " + stringRef);
            var file = split[0].Substring(1);
            var id = split[1];

            if (!_moFiles.ContainsKey(file))
            {
                var filename = "";
                var path_lc_messages = VersionConfig.PathMoFiles.Split('|');
                foreach (var path_lc_message in path_lc_messages)
                {
                    filename = Path.Combine(Installation.Path, path_lc_message, file + ".mo");
                    if (File.Exists(filename))
                    {
                        break;
                    }
                }
                if (filename == "")
                {
                    filename = Path.Combine(Installation.Path, @"res\text\LC_MESSAGES", file + ".mo");
                }
                if (File.Exists(filename))
                    _moFiles[file] = MoReader.ReadFile(filename);
            }
            // for debugging only, since many strings are actually unresolvable for various reasons, e.g. the tank is no longer in the game, or the string is for a pseudo-turret etc.
            // if (!_moFiles[file].ContainsKey(id))
            //     Warnings.Add("Could not resolve the string reference “{0}”.".Fmt(stringRef));
            return _moFiles.ContainsKey(file) && _moFiles[file].ContainsKey(id) ? _moFiles[file][id] : stringRef;
        }
    }

    public sealed class WdCountry
    {
        public string Name { get; set; }

        public IDictionary<string, WdTank> Tanks { get; set; }
        public IDictionary<string, WdChassis> Chassis { get; set; }
        public IDictionary<string, WdTurret> Turrets { get; set; }
        public IDictionary<string, WdEngine> Engines { get; set; }
        public IDictionary<string, WdRadio> Radios { get; set; }
        public IDictionary<string, WdGun> Guns { get; set; }
        public IDictionary<string, WdShell> Shells { get; set; }

        public WdCountry(string name, WdData data, JsonDict tanks, JsonDict engines, JsonDict guns, JsonDict radios, JsonDict shells)
		{
			System.Diagnostics.Debug.WriteLine($"=== DEBUG: WdCountry constructor for {name} ===");
			System.Diagnostics.Debug.WriteLine($"DEBUG: tanks count: {tanks?.Count ?? 0}");
			System.Diagnostics.Debug.WriteLine($"DEBUG: engines type: {engines?.GetType().Name}, has 'shared': {engines?.ContainsKey("shared")}");
			System.Diagnostics.Debug.WriteLine($"DEBUG: guns type: {guns?.GetType().Name}, has 'shared': {guns?.ContainsKey("shared")}");
			System.Diagnostics.Debug.WriteLine($"DEBUG: radios type: {radios?.GetType().Name}, has 'shared': {radios?.ContainsKey("shared")}");
			System.Diagnostics.Debug.WriteLine($"DEBUG: shells type: {shells?.GetType().Name}, count: {shells?.Count}");
			
			Name = name;

			Engines = new Dictionary<string, WdEngine>();
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Processing engines for {name}");
			if (!engines.ContainsKey("shared"))
			{
				System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: engines doesn't have 'shared' key");
				System.Diagnostics.Debug.WriteLine($"DEBUG: Available keys in engines: {string.Join(", ", engines.Keys)}");
			}
			else 
			{
				var sharedEngines = engines["shared"];
				if (!(sharedEngines is JsonDict))
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: engines['shared'] is not JsonDict but {sharedEngines.GetType().Name}");
					System.Diagnostics.Debug.WriteLine($"DEBUG: engines['shared'] value: {sharedEngines}");
				}
				else
				{
					foreach (var kvp in ((JsonDict)sharedEngines).GetDict())
					{
						if (!(kvp.Value is JsonDict))
						{
							System.Diagnostics.Debug.WriteLine($"DEBUG: WARNING: engine '{kvp.Key}' value is not JsonDict but {kvp.Value.GetType().Name}");
							continue;
						}
						var engine = new WdEngine(kvp.Key, kvp.Value.GetDict(), data);
						Engines.Add(kvp.Key, engine);
					}
				}
			}

			Radios = new Dictionary<string, WdRadio>();
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Processing radios for {name}");
			if (!radios.ContainsKey("shared"))
			{
				System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: radios doesn't have 'shared' key");
				System.Diagnostics.Debug.WriteLine($"DEBUG: Available keys in radios: {string.Join(", ", radios.Keys)}");
			}
			else 
			{
				var sharedRadios = radios["shared"];
				if (!(sharedRadios is JsonDict))
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: radios['shared'] is not JsonDict but {sharedRadios.GetType().Name}");
					System.Diagnostics.Debug.WriteLine($"DEBUG: radios['shared'] value: {sharedRadios}");
				}
				else
				{
					foreach (var kvp in ((JsonDict)sharedRadios).GetDict())
					{
						if (!(kvp.Value is JsonDict))
						{
							System.Diagnostics.Debug.WriteLine($"DEBUG: WARNING: radio '{kvp.Key}' value is not JsonDict but {kvp.Value.GetType().Name}");
							continue;
						}
						var radio = new WdRadio(kvp.Key, kvp.Value.GetDict(), data);
						Radios.Add(kvp.Key, radio);
					}
				}
			}

			Shells = new Dictionary<string, WdShell>();
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Processing shells for {name}");
			foreach (var kvp in shells.GetDict())
			{
				string tag = kvp.Key;
				
				if (tag == "icons" || tag.StartsWith("xmlns:") || tag.Contains("xmlref") || tag == "")
					continue;
				
				if (!(kvp.Value is JsonDict))
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: shell '{tag}' value is not JsonDict but {kvp.Value.GetType().Name}");
					System.Diagnostics.Debug.WriteLine($"DEBUG: shell '{tag}' value: {kvp.Value}");
					continue;
				}
				
				var shell = new WdShell(tag, kvp.Value.GetDict(), data);
				Shells.Add(tag, shell);
			}

			Guns = new Dictionary<string, WdGun>();
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Processing guns for {name}");
			if (!guns.ContainsKey("shared"))
			{
				System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: guns doesn't have 'shared' key");
				System.Diagnostics.Debug.WriteLine($"DEBUG: Available keys in guns: {string.Join(", ", guns.Keys)}");
			}
			else 
			{
				var sharedGuns = guns["shared"];
				if (!(sharedGuns is JsonDict))
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: guns['shared'] is not JsonDict but {sharedGuns.GetType().Name}");
					System.Diagnostics.Debug.WriteLine($"DEBUG: guns['shared'] value: {sharedGuns}");
				}
				else
				{
					foreach (var kvp in ((JsonDict)sharedGuns).GetDict())
					{
						try
						{
							if (!(kvp.Value is JsonDict))
							{
								System.Diagnostics.Debug.WriteLine($"DEBUG: WARNING: gun '{kvp.Key}' value is not JsonDict but {kvp.Value.GetType().Name}");
								System.Diagnostics.Debug.WriteLine($"DEBUG: gun '{kvp.Key}' value: {kvp.Value}");
								continue;
							}
							
							var gun = new WdGun(kvp.Key, kvp.Value.GetDict(), data, this);
							Guns.Add(kvp.Key, gun);
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR creating gun '{kvp.Key}': {ex.Message}");
							data.Warnings.Add("Could not load gun data for gun \"{0}\"".Fmt(kvp.Key));
						}
					}
				}
			}

			Tanks = new Dictionary<string, WdTank>();
			Chassis = new Dictionary<string, WdChassis>();
			Turrets = new Dictionary<string, WdTurret>();
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Processing tanks for {name}, count: {tanks.Count}");
			
			foreach (var kvp in tanks.GetDict())
			{
				try
				{
					string tag = kvp.Key;
					
					System.Diagnostics.Debug.WriteLine($"DEBUG: Processing tank key: '{tag}'");
					
					if (tag == "xmlns:xmlref" || tag == "" || tag.StartsWith("xmlns:") || tag.Contains("xmlref") || tag == "icons")
					{
						System.Diagnostics.Debug.WriteLine($"DEBUG: Skipping tank key '{tag}'");
						continue; // this tank is weird; it's the only one which has non-"shared" modules with identical keys to another tank. Ignore it.
					}
					
					if (!(kvp.Value is JsonDict))
					{
						System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR: tank '{tag}' value is not JsonDict but {kvp.Value.GetType().Name}");
						System.Diagnostics.Debug.WriteLine($"DEBUG: tank '{tag}' value: {kvp.Value}");
						continue;
					}
					
					var tank = new WdTank(tag, kvp.Value.GetDict(), this, data);
					Tanks.Add(tank.RawId, tank);
				}
				catch (Exception e)
				{
					System.Diagnostics.Debug.WriteLine($"DEBUG: ERROR processing tank '{kvp.Key}': {e.Message}");
					System.Diagnostics.Debug.WriteLine($"DEBUG: Stack trace: {e.StackTrace}");
					continue;
					throw new WotDataException("Could not parse game data for vehicle \"{0}\"".Fmt(kvp.Key), e);
				}
			}
			
			System.Diagnostics.Debug.WriteLine($"DEBUG: Finished WdCountry for {name}");
			System.Diagnostics.Debug.WriteLine($"DEBUG: Engines: {Engines.Count}, Radios: {Radios.Count}, Shells: {Shells.Count}, Guns: {Guns.Count}, Tanks: {Tanks.Count}");
		}
}

    public sealed class WdTank
    {
        public JsonDict Raw { get; set; }
        public JsonDict RawExtra { get; set; }

        public string RawId { get; set; }
        public WdCountry Country { get; set; }
        public string Id { get { return Country.Name + "-" + RawId; } }
        /// <summary>One of "lightTank", "mediumTank", "heavyTank", "SPG", "AT-SPG", or null if the class was not recognized.</summary>
        public string Class { get; set; }
        public int Tier { get; set; }
        public bool Special { get; set; }
        public bool Collector { get; set; }
        public int Price { get; set; }
        public bool Gold { get; set; }
        public bool Secret { get; set; }

        public HashSet<string> Tags { get; set; }

        public string FullName { get; set; }
        public string ShortName { get; set; }
        public string Description { get; set; }

        public decimal MaxSpeedForward { get; set; }
        public decimal MaxSpeedReverse { get; set; }

        public decimal RepairCost { get; set; }

        public WdHull Hull { get; set; }
        public IList<WdChassis> Chassis { get; set; }
        public IList<WdTurret> Turrets { get; set; }
        public IList<WdEngine> Engines { get; set; }
        public IList<WdRadio> Radios { get; set; }

        /// <summary>
        ///     Gets the top turret by level, price and the number of compatible guns. Note that in the game data files, *all*
        ///     tanks have turrets, even those with turrets that don't rotate. Therefore this property is never null.</summary>
		public WdTurret TopTurret => Turrets?.OrderByDescending(t => t.Level).ThenByDescending(t => t.Price).FirstOrDefault();
		public WdGun TopGun => TopTurret?.Guns?.OrderByDescending(t => t.Level).ThenByDescending(t => t.Price).FirstOrDefault();

        public WdTank(string id, JsonDict json, WdCountry country, WdData data)
        {
            RawId = id;
            Country = country;
            Raw = json;

            var tags1 = Raw["tags"].WdString().Split("\r\n").Select(s => s.Trim()).ToHashSet();
            var tags2 = Raw["tags"].WdString().Split(' ').Select(s => s.Trim()).ToHashSet();
            Tags = tags1.Count > tags2.Count ? tags1 : tags2;
            Special = Tags.Contains("special") ? true : false;
            Collector = Tags.Contains("collectorVehicle") ? true : false;
            Price = Raw["price"] is JsonDict ? Raw["price"][""].WdInt() : Raw["price"].WdInt();
            Gold = Raw["price"] is JsonDict && Raw["price"].ContainsKey("gold");

            if (Tags.Contains("lightTank"))
                Class = "lightTank";
            else if (Tags.Contains("mediumTank"))
                Class = "mediumTank";
            else if (Tags.Contains("heavyTank"))
                Class = "heavyTank";
            else if (Tags.Contains("SPG"))
                Class = "SPG";
            else if (Tags.Contains("AT-SPG"))
                Class = "AT-SPG";
            else
                Class = null;

            Tier = Raw["level"].WdInt();
            Secret = Tags.Contains("secret");
            string scriptsFolder = string.IsNullOrEmpty(data.VersionConfig.PathSourceScripts) ? @"res\scripts" : data.VersionConfig.PathSourceScripts;

            var path = WotFileExporter.CombinePaths(data.Installation.Path, scriptsFolder,
                @"item_defs\vehicles\{0}\{1}.xml".Fmt(Country.Name, id));
            using (var stream = WotFileExporter.GetFileStream(path))
            {
                RawExtra = BxmlReader.ReadFile(stream);
            }

            FullName = data.ResolveString(Raw["userString"].WdString());
            ShortName = Raw.ContainsKey("shortUserString") ? data.ResolveString(Raw["shortUserString"].WdString()) : FullName;
            Description = Raw.ContainsKey("description") ? data.ResolveString(Raw["description"].WdString()) : "";

            MaxSpeedForward = RawExtra["speedLimits"]["forward"].WdDecimal();
            MaxSpeedReverse = RawExtra["speedLimits"]["backward"].WdDecimal();

            RepairCost = RawExtra["repairCost"].WdDecimal();

            Hull = new WdHull(RawExtra["hull"].GetDict());

            Chassis = new List<WdChassis>();
            Turrets = new List<WdTurret>();
            Engines = new List<WdEngine>();
            Radios = new List<WdRadio>();
            // these lists are populated once all the tanks are loaded, since some shared modules occur before the non-shared "definition" of the module.

            foreach (var kvp in RawExtra["chassis"].GetDict())
                if (kvp.Value.GetStringLenientSafe() != "shared" && kvp.Value.Safe[""].GetStringSafe() != "shared" && !kvp.Value.ContainsKey("shared"))
                    Country.Chassis.Add(kvp.Key, new WdChassis(kvp.Key, kvp.Value.GetDict(), data));
            foreach (var kvp in RawExtra["turrets0"].GetDict())
                if (kvp.Value.GetStringLenientSafe() != "shared" && kvp.Value.Safe[""].GetStringSafe() != "shared" && !kvp.Value.ContainsKey("shared"))
                    Country.Turrets.Add(kvp.Key, new WdTurret(kvp.Key, kvp.Value.GetDict(), data));
            // RawExtra["engines"] and RawExtra["radios"] only contain information about unlocks; the rest is contained in separate files parsed in WdCountry.
        }
    }

    public sealed class WdHull
    {
        public JsonDict Raw { get; set; }
        public int Mass { get; set; }
        public int HitPoints { get; set; }
        public int AmmoBayHealth { get; set; }
        public decimal ArmorThicknessFront { get; set; }
        public decimal ArmorThicknessSide { get; set; }
        public decimal ArmorThicknessBack { get; set; }

        public WdHull(JsonDict hull)
        {
            Raw = hull;
            Mass = hull["weight"].WdInt();
            HitPoints = hull["maxHealth"].WdInt();
            AmmoBayHealth = hull["ammoBayHealth"]["maxHealth"].WdInt();
            //var armor = hull["primaryArmor"].WdString().Split(' ').Select(s => hull["armor"][s].WdDecimal()).ToArray();
            var armors = hull["primaryArmor"].WdString().Split(' ');
            ArmorThicknessFront = hull["armor"].ContainsKey(armors[0]) ? hull["armor"][armors[0]].WdDecimal() : 0;
            ArmorThicknessSide = hull["armor"].ContainsKey(armors[1]) ? hull["armor"][armors[1]].WdDecimal() : 0;
            ArmorThicknessBack = hull["armor"].ContainsKey(armors[2]) ? hull["armor"][armors[2]].WdDecimal() : 0;
        }
    }

    public sealed class WdChassis
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public int Level { get; set; }
        public int Price { get; set; }
        public int Mass { get; set; }
        public int HitPoints { get; set; }
        public int MaxLoad { get; set; }
        public int MaxClimbAngle { get; set; }
        public decimal RotationSpeed { get; set; }
        public int TrackArmorThickness { get; set; }

        public decimal TerrainResistanceFirm { get; set; }
        public decimal TerrainResistanceMedium { get; set; }
        public decimal TerrainResistanceSoft { get; set; }

        public bool HasWheels { get; set; }

        public WdChassis(string id, JsonDict chassis, WdData data)
        {
            try
            {
                Raw = chassis;
                Id = id;
                Name = data.ResolveString(chassis["userString"].WdString());

                Level = chassis["level"].WdInt();
                Price = chassis["price"].WdInt();
                Mass = chassis["weight"].WdInt();
                HitPoints = chassis.ContainsKey("maxHealth") ? chassis["maxHealth"].WdInt() : 0;
                MaxLoad = chassis.ContainsKey("maxLoad") ? chassis["maxLoad"].WdInt() : 0;
                MaxClimbAngle = chassis["maxClimbAngle"].WdInt();
                RotationSpeed = chassis["rotationSpeed"].WdDecimal();
                TrackArmorThickness = 0;
                if (chassis.ContainsKey("armor"))
                {
                    string[] ChassisArmors = { "leftTrack", "rightTrack", "armor_9", "armor_15" };
                    IEnumerable<string> ChassisArmor = chassis["armor"].Keys.Intersect<string>(ChassisArmors);
                    if (ChassisArmor.Count() > 0)
                    {
                        foreach (string armor in ChassisArmor)
                        {
                            TrackArmorThickness = Math.Max(TrackArmorThickness, chassis["armor"][armor].WdInt());
                        }
                    }
                    else
                    {
                        data.Warnings.Add("Unable to get chassis armor for {0}".Fmt(chassis["userString"]));
                    }
                }
                var terr = chassis["terrainResistance"].WdString().Split(' ').Select(s => decimal.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture)).ToList();
                TerrainResistanceFirm = terr[0];
                TerrainResistanceMedium = terr[1];
                TerrainResistanceSoft = terr[2];

                HasWheels = !chassis.ContainsKey("tracks");
            }
            catch
            {
                return;
            }

        }
    }

    public sealed class WdTurret
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public int Level { get; set; }
        public int Price { get; set; }
        public int Mass { get; set; }
        public int HitPoints { get; set; }
        public decimal RotationSpeed { get; set; }
        public decimal VisionDistance { get; set; }

        public decimal ArmorThicknessFront { get; set; }
        public decimal ArmorThicknessSide { get; set; }
        public decimal ArmorThicknessBack { get; set; }

        public bool Rotates { get { return Guns.Any(g => g.TurretRotates); } }

        public IList<WdGun> Guns { get; set; }

        public WdTurret(string id, JsonDict turret, WdData data)
        {
            Raw = turret;
            Id = id;
            Name = data.ResolveString(turret["userString"].WdString());

            Level = turret["level"].WdInt();
            Price = turret["price"].WdInt();
            Mass = turret["weight"].WdInt();
            HitPoints = turret["maxHealth"].WdInt();
            RotationSpeed = turret["rotationSpeed"].WdDecimal();
            VisionDistance = turret["circularVisionRadius"].WdDecimal();

            try
            {
                var armor = turret["primaryArmor"].WdString().Split(' ').Select(s => turret["armor"][s].WdDecimal()).ToArray();
                ArmorThicknessFront = armor[0];
                ArmorThicknessSide = armor[1];
                ArmorThicknessBack = armor[2];
            }
            catch
            {
                ArmorThicknessFront = ArmorThicknessSide = ArmorThicknessBack = 0;
            }

            Guns = new List<WdGun>();
        }
    }

    public sealed class WdEngine
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public int Level { get; set; }
        public int Price { get; set; }
        public int Mass { get; set; }
        public decimal HitPoints { get; set; }
        public int Power { get; set; }
        public decimal FireStartChance { get; set; }

        public WdEngine(string id, JsonDict engine, WdData data)
        {
            Raw = engine;
            Id = id;
            Name = data.ResolveString(engine["userString"].WdString());

            Level = engine["level"].WdInt();
            Price = engine["price"].WdInt();
            Mass = engine["weight"].WdInt();
            HitPoints = engine["maxHealth"].WdDecimal();
            Power = engine["power"].WdInt();
            FireStartChance = engine["fireStartingChance"].WdDecimal();
        }
    }

    public sealed class WdRadio
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public int Level { get; set; }
        public int Price { get; set; }
        public int Mass { get; set; }
        public int HitPoints { get; set; }
        public int Distance { get; set; }

        public WdRadio(string id, JsonDict radio, WdData data)
        {
            Raw = radio;
            Id = id;
            Name = data.ResolveString(radio["userString"].WdString());

            Level = (radio.ContainsKey("Level") ? radio["Level"] : radio["level"]).WdInt(); // "Level" used in older clients for at least one radio
            Price = radio["price"].WdInt();
            Mass = radio["weight"].WdInt();
            HitPoints = radio["maxHealth"].WdInt();
            Distance = radio["distance"].WdInt();
        }
    }

    public sealed class WdGun
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public int Level { get; set; }
        public int Price { get; set; }
        public int Mass { get; set; }
        public int HitPoints { get; set; }

        public decimal PitchDownLimit { get; set; }
        public decimal PitchUpLimit { get; set; }
        public decimal YawLeftLimit { get; set; }
        public decimal YawRightLimit { get; set; }

        public decimal RotationSpeed { get; set; }
        public decimal ReloadTime { get; set; }
        public int MaxAmmo { get; set; }
        public decimal AimTime { get; set; }

        public bool HasDrum { get; set; }
        public bool TurretRotates { get { return YawLeftLimit == -180 && YawRightLimit == 180; } }

        public IList<WdShell> Shells { get; set; }

        public WdGun(string id, JsonDict gun, WdData data, WdCountry country)
        {
            Raw = gun;
            Id = id;
			Name = WdDataHelpers.SafeStr(gun["userString"]);
            Shells = new List<WdShell>();
            PitchUpLimit = -999;
            PitchDownLimit = -999;
            UpdateFrom(gun, country, initialize: true);
        }

        internal WdGun Clone()
        {
            var result = (WdGun) MemberwiseClone();
            result.Shells = result.Shells.Select(s => s.Clone()).ToList(); // deep clone all the shell data
            return result;
        }

        internal void Validate()
        {
            if (PitchUpLimit == -999 || PitchDownLimit == -999)
                throw new Exception("No pitch up/down information found for this gun.");
        }

        /// <remarks>
        ///     This method requires the shell data to already be fully loaded.</remarks>
        /// <param name="initialize">
        ///     Forces all parameters to be loaded, producing an exception if anything important is missing.</param>
        internal void UpdateFrom(JsonDict gun, WdCountry country, bool initialize = false)
        {
            // Not sure if this is the easiest way to correctly load the data on guns... but seems to do the job
            // The idea appears to be that there is a base gun definition, but each specific turret may override certain gun parameters

            if (initialize || gun.ContainsKey("level"))
                Level = gun["level"].WdInt();
            if (initialize || gun.ContainsKey("price"))
                Price = gun["price"].WdInt();
            if (initialize || gun.ContainsKey("mass"))
                Mass = gun["weight"].WdInt();
            if (initialize || gun.ContainsKey("maxHealth"))
                HitPoints = gun["maxHealth"].WdInt();

            if (initialize || gun.ContainsKey("pitchLimits"))
            {
                if (gun["pitchLimits"] is JsonDict)
                {
                    // new-style limits in 0.9.9 and later
                    if (gun["pitchLimits"].ContainsKey("minPitch"))
                        PitchUpLimit = (gun["pitchLimits"]["minPitch"].WdString() == "") ? 0 : -gun["pitchLimits"]["minPitch"].WdString().Split(' ').Select(x => (x == "") ? 0 : decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).Min();
                    if (gun["pitchLimits"].ContainsKey("maxPitch"))
                        PitchDownLimit = (gun["pitchLimits"]["maxPitch"].WdString() == "") ? 0 : -gun["pitchLimits"]["maxPitch"].WdString().Split(' ').Select(x => (x == "") ? 0 : decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).Max();
                }
                else
                {
                    // old-style limits before 0.9.9
                    var parts = gun["pitchLimits"].WdString().Split(' ').Select(x => decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                    PitchUpLimit = -parts[0];
                    PitchDownLimit = -parts[1];
                }
            }
            if (gun.ContainsKey("turretYawLimits")) // earlier game versions have this in the turret data
            {
                var parts = gun["turretYawLimits"].WdString().Split(' ').Select(x => decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
                YawLeftLimit = parts[0]; // not too sure about which is which
                YawRightLimit = parts[1];
            }

            if (initialize || gun.ContainsKey("rotationSpeed"))
                RotationSpeed = gun["rotationSpeed"].WdDecimal();
            if (initialize || gun.ContainsKey("reloadTime"))
                ReloadTime = gun["reloadTime"].WdDecimal();
            if (initialize || gun.ContainsKey("maxAmmo"))
                MaxAmmo = gun["maxAmmo"].WdInt();
            if (initialize || gun.ContainsKey("aimingTime"))
                AimTime = gun["aimingTime"].WdDecimal();

            if (initialize)
                HasDrum = gun.ContainsKey("clip");
            else if (gun.ContainsKey("clip"))
                HasDrum = true;

            // Load the shell types - not sure how the overrides are supposed to work here, so just clear the entire list in that case
            if (gun.ContainsKey("shots"))
            {
                Shells.Clear();
                foreach (var kvp in gun["shots"].GetDict())
                {
                    var shell = country.Shells[kvp.Key].Clone();
                    shell.AddGunSpecific(kvp.Value.GetDict());
                    Shells.Add(shell);
                }
            }
        }
    }

    public sealed class WdShell
    {
        public JsonDict Raw { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }

        public string Kind { get; set; }
        public decimal Caliber { get; set; }
        public int DamageArmor { get; set; }
        public decimal DamageDevices { get; set; }
        public int Speed { get; set; }
        public decimal PenetrationArmor { get; set; }

        public int Price { get; set; }
        public bool Gold { get; set; }

        public WdShell(string id, JsonDict shell, WdData data)
        {
            Raw = shell;
            Id = id;
            Name = data.ResolveString(shell["userString"].WdString());

            Kind = shell["kind"].WdString();
            Caliber = shell["caliber"].WdDecimal();
            var damage_armor = shell["damage"]["armor"];
            if (damage_armor is JsonNumber)
            {
                DamageArmor = damage_armor.WdInt();
            }
            else
            {
                // For shells with variable damage the maximum damage is selected.
                //DamageArmor = damage_armor.WdString().Split(' ').Select(int.Parse).ToArray()[0];
                DamageArmor = Convert.ToInt32(damage_armor.WdString().Split(' ')[0]);
            }

            var damage_device = shell["damage"]["devices"];
            if (damage_device is JsonNumber)
            {
                DamageDevices = damage_device.WdDecimal();
            }
            else
            {
                // For shells with variable damage the maximum damage is selected.
                //DamageDevices = damage_device.WdString().Split(' ').Select(x => decimal.Parse(x, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray()[0];
                DamageDevices = Convert.ToDecimal(damage_device.WdString().Split(' ')[0], CultureInfo.InvariantCulture);
            }

            Price = shell["price"].WdInt();
            Gold = shell["price"] is JsonDict && shell["price"].ContainsKey("gold");
        }

        internal WdShell Clone()
        {
            var result = (WdShell) MemberwiseClone();
            return result;
        }

        internal void AddGunSpecific(JsonDict shell)
        {
            Speed = shell["speed"].WdInt();
            PenetrationArmor = decimal.Parse(shell["piercingPower"].WdString().Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }

	static class WdDataHelpers
	{
		public static int WdInt(this JsonValue value)
		{
			if (value is JsonDict)
				value = value[""];
			return value.GetInt(NumericConversionOptions.AllowConversionFromString | NumericConversionOptions.AllowZeroFractionToInteger);
		}

		public static decimal WdDecimal(this JsonValue value)
		{
			if (value is JsonDict)
				value = value[""];
			return value.GetDecimal(NumericConversionOptions.AllowConversionFromString);
		}

		public static string WdString(this JsonValue value)
		{
			if (value is JsonDict)
				value = value[""];
			return value.GetString();
		}

		public static string SafeStr(JsonValue val) => WdString(val);
	}
}
