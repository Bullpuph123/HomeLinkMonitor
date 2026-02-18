namespace HomeLinkMonitor.Services;

/// <summary>
/// Extracts geographic location from ISP/backbone router hostnames.
/// Recognizes IATA airport codes, CLLI city codes, and full city names
/// commonly embedded in router hostnames by major carriers.
/// </summary>
public static class HostnameGeoParser
{
    public record ParsedLocation(string City, string Region, string Country, double Lat, double Lon);

    public static ParsedLocation? TryParse(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname) || hostname == "*")
            return null;

        var lower = hostname.ToLowerInvariant();
        var segments = lower.Split('.', '-', '_');

        // Pass 1: CLLI codes (most specific — 4-letter city + 2-letter state + optional digits)
        foreach (var seg in segments)
        {
            if (seg.Length >= 6)
            {
                var city4 = seg[..4];
                var state2 = seg[4..6];
                var rest = seg[6..];
                if (ClliCities.TryGetValue(city4, out var clli)
                    && UsStates.Contains(state2)
                    && (rest.Length == 0 || rest.All(char.IsDigit)))
                {
                    return new ParsedLocation(clli.City, state2.ToUpperInvariant(), "United States", clli.Lat, clli.Lon);
                }
            }
        }

        // Pass 2: Full city names as segments (Level3 style: "Dallas1", "Chicago2", "SanJose1")
        foreach (var seg in segments)
        {
            foreach (var (name, loc) in CityNames)
            {
                if (seg.StartsWith(name)
                    && (seg.Length == name.Length || seg[name.Length..].All(char.IsDigit)))
                {
                    return loc;
                }
            }
        }

        // Pass 3: 3-letter IATA / city codes (e.g., "iad08", "dfw12", "nyc4")
        foreach (var seg in segments)
        {
            if (seg.Length >= 3)
            {
                var code = seg[..3];
                var rest = seg[3..];
                if (IataCodes.TryGetValue(code, out var loc)
                    && (rest.Length == 0 || rest.All(char.IsDigit)))
                {
                    return loc;
                }
            }
        }

        return null;
    }

    // 3-letter IATA airport codes and common city abbreviations used by ISPs
    private static readonly Dictionary<string, ParsedLocation> IataCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // --- US major ---
        ["atl"] = new("Atlanta", "GA", "United States", 33.749, -84.388),
        ["aus"] = new("Austin", "TX", "United States", 30.267, -97.743),
        ["bna"] = new("Nashville", "TN", "United States", 36.163, -86.781),
        ["bos"] = new("Boston", "MA", "United States", 42.360, -71.059),
        ["bwi"] = new("Baltimore", "MD", "United States", 39.290, -76.612),
        ["cle"] = new("Cleveland", "OH", "United States", 41.499, -81.694),
        ["clt"] = new("Charlotte", "NC", "United States", 35.227, -80.843),
        ["cmh"] = new("Columbus", "OH", "United States", 39.962, -82.999),
        ["cvg"] = new("Cincinnati", "OH", "United States", 39.100, -84.512),
        ["dca"] = new("Washington", "DC", "United States", 38.907, -77.037),
        ["den"] = new("Denver", "CO", "United States", 39.739, -104.990),
        ["dfw"] = new("Dallas", "TX", "United States", 32.777, -96.797),
        ["dtw"] = new("Detroit", "MI", "United States", 42.331, -83.046),
        ["ewr"] = new("Newark", "NJ", "United States", 40.736, -74.172),
        ["iad"] = new("Washington", "DC", "United States", 38.907, -77.037),
        ["iah"] = new("Houston", "TX", "United States", 29.760, -95.370),
        ["ind"] = new("Indianapolis", "IN", "United States", 39.768, -86.158),
        ["jax"] = new("Jacksonville", "FL", "United States", 30.332, -81.656),
        ["jfk"] = new("New York", "NY", "United States", 40.713, -74.006),
        ["las"] = new("Las Vegas", "NV", "United States", 36.169, -115.140),
        ["lax"] = new("Los Angeles", "CA", "United States", 34.052, -118.244),
        ["mci"] = new("Kansas City", "MO", "United States", 39.100, -94.578),
        ["mia"] = new("Miami", "FL", "United States", 25.762, -80.192),
        ["mke"] = new("Milwaukee", "WI", "United States", 43.039, -87.907),
        ["msp"] = new("Minneapolis", "MN", "United States", 44.978, -93.265),
        ["msy"] = new("New Orleans", "LA", "United States", 29.951, -90.072),
        ["oma"] = new("Omaha", "NE", "United States", 41.256, -95.934),
        ["ord"] = new("Chicago", "IL", "United States", 41.878, -87.630),
        ["pdx"] = new("Portland", "OR", "United States", 45.505, -122.675),
        ["phl"] = new("Philadelphia", "PA", "United States", 39.953, -75.164),
        ["phx"] = new("Phoenix", "AZ", "United States", 33.449, -112.074),
        ["pit"] = new("Pittsburgh", "PA", "United States", 40.441, -79.996),
        ["rdu"] = new("Raleigh", "NC", "United States", 35.780, -78.639),
        ["san"] = new("San Diego", "CA", "United States", 32.716, -117.161),
        ["sat"] = new("San Antonio", "TX", "United States", 29.425, -98.495),
        ["sea"] = new("Seattle", "WA", "United States", 47.606, -122.332),
        ["sfo"] = new("San Francisco", "CA", "United States", 37.775, -122.418),
        ["sjc"] = new("San Jose", "CA", "United States", 37.339, -121.895),
        ["slc"] = new("Salt Lake City", "UT", "United States", 40.761, -111.891),
        ["stl"] = new("St. Louis", "MO", "United States", 38.627, -90.199),
        ["tpa"] = new("Tampa", "FL", "United States", 27.951, -82.458),

        // --- US common non-IATA abbreviations ---
        ["chi"] = new("Chicago", "IL", "United States", 41.878, -87.630),
        ["nyc"] = new("New York", "NY", "United States", 40.713, -74.006),
        ["dal"] = new("Dallas", "TX", "United States", 32.777, -96.797),
        ["hou"] = new("Houston", "TX", "United States", 29.760, -95.370),
        ["was"] = new("Washington", "DC", "United States", 38.907, -77.037),

        // --- International ---
        ["ams"] = new("Amsterdam", "", "Netherlands", 52.370, 4.895),
        ["arn"] = new("Stockholm", "", "Sweden", 59.329, 18.069),
        ["bru"] = new("Brussels", "", "Belgium", 50.850, 4.352),
        ["cdg"] = new("Paris", "", "France", 48.857, 2.352),
        ["cph"] = new("Copenhagen", "", "Denmark", 55.676, 12.569),
        ["dub"] = new("Dublin", "", "Ireland", 53.350, -6.260),
        ["fra"] = new("Frankfurt", "", "Germany", 50.110, 8.682),
        ["gru"] = new("São Paulo", "", "Brazil", -23.551, -46.633),
        ["hel"] = new("Helsinki", "", "Finland", 60.170, 24.938),
        ["hkg"] = new("Hong Kong", "", "Hong Kong", 22.320, 114.169),
        ["icn"] = new("Seoul", "", "South Korea", 37.566, 126.978),
        ["kix"] = new("Osaka", "", "Japan", 34.694, 135.502),
        ["lhr"] = new("London", "", "United Kingdom", 51.507, -0.128),
        ["lis"] = new("Lisbon", "", "Portugal", 38.722, -9.139),
        ["lon"] = new("London", "", "United Kingdom", 51.507, -0.128),
        ["mad"] = new("Madrid", "", "Spain", 40.417, -3.704),
        ["mrs"] = new("Marseille", "", "France", 43.296, 5.370),
        ["muc"] = new("Munich", "", "Germany", 48.137, 11.576),
        ["nrt"] = new("Tokyo", "", "Japan", 35.682, 139.692),
        ["osl"] = new("Oslo", "", "Norway", 59.914, 10.752),
        ["par"] = new("Paris", "", "France", 48.857, 2.352),
        ["prg"] = new("Prague", "", "Czech Republic", 50.075, 14.437),
        ["sin"] = new("Singapore", "", "Singapore", 1.352, 103.820),
        ["sto"] = new("Stockholm", "", "Sweden", 59.329, 18.069),
        ["syd"] = new("Sydney", "", "Australia", -33.869, 151.209),
        ["tyo"] = new("Tokyo", "", "Japan", 35.682, 139.692),
        ["vie"] = new("Vienna", "", "Austria", 48.208, 16.372),
        ["waw"] = new("Warsaw", "", "Poland", 52.230, 21.012),
        ["zrh"] = new("Zurich", "", "Switzerland", 47.377, 8.540),
        ["mil"] = new("Milan", "", "Italy", 45.464, 9.190),
        ["sao"] = new("São Paulo", "", "Brazil", -23.551, -46.633),
        ["yyz"] = new("Toronto", "", "Canada", 43.653, -79.383),
        ["yul"] = new("Montreal", "", "Canada", 45.502, -73.567),
        ["yvr"] = new("Vancouver", "", "Canada", 49.283, -123.121),
        ["mex"] = new("Mexico City", "", "Mexico", 19.433, -99.133),
    };

    // CLLI 4-letter city codes (US telco standard)
    private static readonly Dictionary<string, (string City, double Lat, double Lon)> ClliCities =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["atln"] = ("Atlanta", 33.749, -84.388),
        ["atlx"] = ("Atlanta", 33.749, -84.388),
        ["ausn"] = ("Austin", 30.267, -97.743),
        ["bflo"] = ("Buffalo", 42.887, -78.879),
        ["bltm"] = ("Baltimore", 39.290, -76.612),
        ["bstn"] = ("Boston", 42.360, -71.059),
        ["chcg"] = ("Chicago", 41.878, -87.630),
        ["chrl"] = ("Charlotte", 35.227, -80.843),
        ["cinc"] = ("Cincinnati", 39.100, -84.512),
        ["clev"] = ("Cleveland", 41.499, -81.694),
        ["clmb"] = ("Columbus", 39.962, -82.999),
        ["denv"] = ("Denver", 39.739, -104.990),
        ["dlls"] = ("Dallas", 32.777, -96.797),
        ["dllx"] = ("Dallas", 32.777, -96.797),
        ["dtrt"] = ("Detroit", 42.331, -83.046),
        ["hstx"] = ("Houston", 29.760, -95.370),
        ["hstn"] = ("Houston", 29.760, -95.370),
        ["jcsn"] = ("Jacksonville", 30.332, -81.656),
        ["jcvl"] = ("Jacksonville", 30.332, -81.656),
        ["kscy"] = ("Kansas City", 39.100, -94.578),
        ["lsan"] = ("Los Angeles", 34.052, -118.244),
        ["lsvg"] = ("Las Vegas", 36.169, -115.140),
        ["lsvn"] = ("Las Vegas", 36.169, -115.140),
        ["miam"] = ("Miami", 25.762, -80.192),
        ["milw"] = ("Milwaukee", 43.039, -87.907),
        ["mnps"] = ("Minneapolis", 44.978, -93.265),
        ["nsvl"] = ("Nashville", 36.163, -86.781),
        ["nwrk"] = ("Newark", 40.736, -74.172),
        ["nycm"] = ("New York", 40.713, -74.006),
        ["okcy"] = ("Oklahoma City", 35.468, -97.516),
        ["omah"] = ("Omaha", 41.256, -95.934),
        ["phla"] = ("Philadelphia", 39.953, -75.164),
        ["phnx"] = ("Phoenix", 33.449, -112.074),
        ["pitt"] = ("Pittsburgh", 40.441, -79.996),
        ["ptld"] = ("Portland", 45.505, -122.675),
        ["rlgh"] = ("Raleigh", 35.780, -78.639),
        ["sant"] = ("San Antonio", 29.425, -98.495),
        ["slkc"] = ("Salt Lake City", 40.761, -111.891),
        ["sndg"] = ("San Diego", 32.716, -117.161),
        ["snfc"] = ("San Francisco", 37.775, -122.418),
        ["snjs"] = ("San Jose", 37.339, -121.895),
        ["snjx"] = ("San Jose", 37.339, -121.895),
        ["stls"] = ("St. Louis", 38.627, -90.199),
        ["sttl"] = ("Seattle", 47.606, -122.332),
        ["tamp"] = ("Tampa", 27.951, -82.458),
        ["wash"] = ("Washington", 38.907, -77.037),
    };

    // Full city names (Level3/Lumen style: "Dallas1", "Chicago2", "SanJose1")
    private static readonly (string Name, ParsedLocation Loc)[] CityNames =
    [
        // Multi-word (check longer names first to avoid partial match)
        ("saltlakecity", new("Salt Lake City", "UT", "United States", 40.761, -111.891)),
        ("kansascity", new("Kansas City", "MO", "United States", 39.100, -94.578)),
        ("oklahomacity", new("Oklahoma City", "OK", "United States", 35.468, -97.516)),
        ("losangeles", new("Los Angeles", "CA", "United States", 34.052, -118.244)),
        ("sanfrancisco", new("San Francisco", "CA", "United States", 37.775, -122.418)),
        ("sanjose", new("San Jose", "CA", "United States", 37.339, -121.895)),
        ("sandiego", new("San Diego", "CA", "United States", 32.716, -117.161)),
        ("sanantonio", new("San Antonio", "TX", "United States", 29.425, -98.495)),
        ("newyork", new("New York", "NY", "United States", 40.713, -74.006)),
        ("neworleans", new("New Orleans", "LA", "United States", 29.951, -90.072)),
        ("lasvegas", new("Las Vegas", "NV", "United States", 36.169, -115.140)),
        ("fortworth", new("Fort Worth", "TX", "United States", 32.755, -97.331)),
        ("jacksonville", new("Jacksonville", "FL", "United States", 30.332, -81.656)),
        ("indianapolis", new("Indianapolis", "IN", "United States", 39.768, -86.158)),
        ("minneapolis", new("Minneapolis", "MN", "United States", 44.978, -93.265)),
        ("philadelphia", new("Philadelphia", "PA", "United States", 39.953, -75.164)),
        ("sacramento", new("Sacramento", "CA", "United States", 38.582, -121.494)),
        ("washington", new("Washington", "DC", "United States", 38.907, -77.037)),
        // Single-word
        ("atlanta", new("Atlanta", "GA", "United States", 33.749, -84.388)),
        ("austin", new("Austin", "TX", "United States", 30.267, -97.743)),
        ("baltimore", new("Baltimore", "MD", "United States", 39.290, -76.612)),
        ("boston", new("Boston", "MA", "United States", 42.360, -71.059)),
        ("buffalo", new("Buffalo", "NY", "United States", 42.887, -78.879)),
        ("charlotte", new("Charlotte", "NC", "United States", 35.227, -80.843)),
        ("chicago", new("Chicago", "IL", "United States", 41.878, -87.630)),
        ("cincinnati", new("Cincinnati", "OH", "United States", 39.100, -84.512)),
        ("cleveland", new("Cleveland", "OH", "United States", 41.499, -81.694)),
        ("columbus", new("Columbus", "OH", "United States", 39.962, -82.999)),
        ("dallas", new("Dallas", "TX", "United States", 32.777, -96.797)),
        ("denver", new("Denver", "CO", "United States", 39.739, -104.990)),
        ("detroit", new("Detroit", "MI", "United States", 42.331, -83.046)),
        ("houston", new("Houston", "TX", "United States", 29.760, -95.370)),
        ("miami", new("Miami", "FL", "United States", 25.762, -80.192)),
        ("milwaukee", new("Milwaukee", "WI", "United States", 43.039, -87.907)),
        ("nashville", new("Nashville", "TN", "United States", 36.163, -86.781)),
        ("newark", new("Newark", "NJ", "United States", 40.736, -74.172)),
        ("omaha", new("Omaha", "NE", "United States", 41.256, -95.934)),
        ("orlando", new("Orlando", "FL", "United States", 28.538, -81.379)),
        ("phoenix", new("Phoenix", "AZ", "United States", 33.449, -112.074)),
        ("pittsburgh", new("Pittsburgh", "PA", "United States", 40.441, -79.996)),
        ("portland", new("Portland", "OR", "United States", 45.505, -122.675)),
        ("raleigh", new("Raleigh", "NC", "United States", 35.780, -78.639)),
        ("seattle", new("Seattle", "WA", "United States", 47.606, -122.332)),
        ("tampa", new("Tampa", "FL", "United States", 27.951, -82.458)),
        // International
        ("amsterdam", new("Amsterdam", "", "Netherlands", 52.370, 4.895)),
        ("frankfurt", new("Frankfurt", "", "Germany", 50.110, 8.682)),
        ("london", new("London", "", "United Kingdom", 51.507, -0.128)),
        ("paris", new("Paris", "", "France", 48.857, 2.352)),
        ("singapore", new("Singapore", "", "Singapore", 1.352, 103.820)),
        ("stockholm", new("Stockholm", "", "Sweden", 59.329, 18.069)),
        ("sydney", new("Sydney", "", "Australia", -33.869, 151.209)),
        ("tokyo", new("Tokyo", "", "Japan", 35.682, 139.692)),
        ("toronto", new("Toronto", "", "Canada", 43.653, -79.383)),
        ("vancouver", new("Vancouver", "", "Canada", 49.283, -123.121)),
        ("zurich", new("Zurich", "", "Switzerland", 47.377, 8.540)),
    ];

    private static readonly HashSet<string> UsStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "al","ak","az","ar","ca","co","ct","dc","de","fl","ga","hi","id","il","in",
        "ia","ks","ky","la","me","md","ma","mi","mn","ms","mo","mt","ne","nv","nh",
        "nj","nm","ny","nc","nd","oh","ok","or","pa","ri","sc","sd","tn","tx","ut",
        "vt","va","wa","wv","wi","wy"
    };
}
