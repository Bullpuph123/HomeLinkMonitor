namespace HomeLinkMonitor.Services;

/// <summary>
/// Extracts geographic location from ISP/backbone router hostnames.
/// Supports city.state patterns (Comcast, etc.), CLLI codes, IATA codes,
/// and standalone city names (Level3/Lumen).
/// </summary>
public static class HostnameGeoParser
{
    public record ParsedLocation(string City, string Region, string Country, double Lat, double Lon);

    public static ParsedLocation? TryParse(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname) || hostname == "*")
            return null;

        var segments = hostname.ToLowerInvariant().Split('.', '-', '_');

        // Pass 1: CLLI codes (e.g., "snjsca04", "chcgil02")
        foreach (var seg in segments)
        {
            if (seg.Length >= 6)
            {
                var city4 = seg[..4];
                var state2 = seg[4..6];
                var rest = seg[6..];
                if (ClliCodes.TryGetValue(city4, out var clli)
                    && UsStates.Contains(state2)
                    && (rest.Length == 0 || rest.All(char.IsDigit)))
                {
                    return new ParsedLocation(clli.City, state2.ToUpperInvariant(), "United States", clli.Lat, clli.Lon);
                }
            }
        }

        // Pass 2: City.State adjacent segment pattern (e.g., "chico.ca", "sunnyvale.ca", "great-oaks.ca")
        for (int i = 0; i < segments.Length - 1; i++)
        {
            var stateSeg = segments[i + 1];
            if (stateSeg.Length != 2 || !UsStates.Contains(stateSeg))
                continue;

            var citySeg = StripTrailingDigits(segments[i]);
            if (citySeg.Length < 3 || !citySeg.All(char.IsLetter))
                continue;

            // Direct match: "chico" + "ca"
            if (UsCities.TryGetValue((citySeg, stateSeg), out var loc))
                return new ParsedLocation(loc.Name, stateSeg.ToUpperInvariant(), "United States", loc.Lat, loc.Lon);

            // Join with previous segment for compound names: "great"+"oaks" + "ca"
            if (i > 0)
            {
                var prevSeg = StripTrailingDigits(segments[i - 1]);
                if (prevSeg.Length >= 2 && prevSeg.All(char.IsLetter))
                {
                    var joined = prevSeg + citySeg;
                    if (UsCities.TryGetValue((joined, stateSeg), out var jloc))
                        return new ParsedLocation(jloc.Name, stateSeg.ToUpperInvariant(), "United States", jloc.Lat, jloc.Lon);
                }

                // Join 3 segments: "san"+"luis"+"obispo" + "ca"
                if (i > 1)
                {
                    var prev2 = StripTrailingDigits(segments[i - 2]);
                    if (prev2.Length >= 2 && prev2.All(char.IsLetter))
                    {
                        var joined3 = prev2 + StripTrailingDigits(segments[i - 1]) + citySeg;
                        if (UsCities.TryGetValue((joined3, stateSeg), out var j3loc))
                            return new ParsedLocation(j3loc.Name, stateSeg.ToUpperInvariant(), "United States", j3loc.Lat, j3loc.Lon);
                    }
                }
            }
        }

        // Pass 3: Standalone city names (Level3 style: "Dallas1", "SanJose1")
        foreach (var seg in segments)
        {
            var stripped = StripTrailingDigits(seg);
            if (stripped.Length < 4) continue;

            foreach (var (name, loc) in StandaloneCityNames)
            {
                if (stripped == name)
                    return loc;
            }
        }

        // Pass 4: IATA 3-letter codes (e.g., "iad08", "dfw12")
        foreach (var seg in segments)
        {
            if (seg.Length < 3) continue;
            var code = seg[..3];
            var rest = seg[3..];
            if (IataCodes.TryGetValue(code, out var loc)
                && (rest.Length == 0 || rest.All(char.IsDigit)))
            {
                return loc;
            }
        }

        return null;
    }

    private static string StripTrailingDigits(string s)
    {
        int end = s.Length;
        while (end > 0 && char.IsDigit(s[end - 1]))
            end--;
        return s[..end];
    }

    // -----------------------------------------------------------------------
    //  US city database: (normalized_name, state) → (display_name, lat, lon)
    //  Normalized = lowercase, no spaces/hyphens (e.g., "sanjose", "greatolks")
    // -----------------------------------------------------------------------
    private static readonly Dictionary<(string, string), (string Name, double Lat, double Lon)> UsCities =
        BuildUsCities();

    private static Dictionary<(string, string), (string Name, double Lat, double Lon)> BuildUsCities()
    {
        var dict = new Dictionary<(string, string), (string, double, double)>();

        // All entries are (normalized_lowercase_name, state, DisplayName, lat, lon)
        (string Key, string State, string Name, double Lat, double Lon)[] data =
        [
            // === ALABAMA ===
            ("birmingham", "al", "Birmingham", 33.521, -86.802),
            ("huntsville", "al", "Huntsville", 34.730, -86.586),
            ("mobile", "al", "Mobile", 30.695, -88.040),
            ("montgomery", "al", "Montgomery", 32.379, -86.309),
            // === ALASKA ===
            ("anchorage", "ak", "Anchorage", 61.218, -149.900),
            ("fairbanks", "ak", "Fairbanks", 64.837, -147.716),
            ("juneau", "ak", "Juneau", 58.302, -134.420),
            // === ARIZONA ===
            ("chandler", "az", "Chandler", 33.303, -111.841),
            ("flagstaff", "az", "Flagstaff", 35.198, -111.651),
            ("gilbert", "az", "Gilbert", 33.353, -111.789),
            ("glendale", "az", "Glendale", 33.539, -112.186),
            ("mesa", "az", "Mesa", 33.415, -111.831),
            ("peoria", "az", "Peoria", 33.581, -112.238),
            ("phoenix", "az", "Phoenix", 33.449, -112.074),
            ("scottsdale", "az", "Scottsdale", 33.494, -111.926),
            ("tempe", "az", "Tempe", 33.415, -111.909),
            ("tucson", "az", "Tucson", 32.222, -110.975),
            ("yuma", "az", "Yuma", 32.693, -114.628),
            // === ARKANSAS ===
            ("fayetteville", "ar", "Fayetteville", 36.063, -94.157),
            ("fortsmith", "ar", "Fort Smith", 35.386, -94.399),
            ("littlerock", "ar", "Little Rock", 34.747, -92.290),
            // === CALIFORNIA ===
            ("alameda", "ca", "Alameda", 37.765, -122.242),
            ("alhambra", "ca", "Alhambra", 34.095, -118.127),
            ("anaheim", "ca", "Anaheim", 33.836, -117.914),
            ("antioch", "ca", "Antioch", 38.005, -121.806),
            ("arcadia", "ca", "Arcadia", 34.140, -118.036),
            ("bakersfield", "ca", "Bakersfield", 35.373, -119.019),
            ("berkeley", "ca", "Berkeley", 37.871, -122.273),
            ("brea", "ca", "Brea", 33.917, -117.900),
            ("burbank", "ca", "Burbank", 34.181, -118.309),
            ("burlingame", "ca", "Burlingame", 37.577, -122.348),
            ("campbell", "ca", "Campbell", 37.287, -121.950),
            ("carlsbad", "ca", "Carlsbad", 33.159, -117.351),
            ("carson", "ca", "Carson", 33.831, -118.282),
            ("chico", "ca", "Chico", 39.729, -121.837),
            ("chinohills", "ca", "Chino Hills", 33.994, -117.729),
            ("chulavista", "ca", "Chula Vista", 32.640, -117.084),
            ("citrusheights", "ca", "Citrus Heights", 38.707, -121.281),
            ("clovis", "ca", "Clovis", 36.825, -119.703),
            ("colton", "ca", "Colton", 34.074, -117.313),
            ("compton", "ca", "Compton", 33.896, -118.220),
            ("concord", "ca", "Concord", 37.978, -122.031),
            ("corona", "ca", "Corona", 33.875, -117.566),
            ("costamesa", "ca", "Costa Mesa", 33.641, -117.919),
            ("cupertino", "ca", "Cupertino", 37.323, -122.032),
            ("dalycity", "ca", "Daly City", 37.688, -122.470),
            ("davis", "ca", "Davis", 38.545, -121.741),
            ("dublin", "ca", "Dublin", 37.702, -121.936),
            ("elcajon", "ca", "El Cajon", 32.795, -116.963),
            ("elkmonte", "ca", "El Monte", 34.069, -118.028),
            ("elkgrove", "ca", "Elk Grove", 38.409, -121.372),
            ("encinitas", "ca", "Encinitas", 33.037, -117.292),
            ("escondido", "ca", "Escondido", 33.119, -117.086),
            ("eureka", "ca", "Eureka", 40.802, -124.164),
            ("fairfield", "ca", "Fairfield", 38.249, -122.040),
            ("folsom", "ca", "Folsom", 38.678, -121.176),
            ("fontana", "ca", "Fontana", 34.092, -117.435),
            ("fremont", "ca", "Fremont", 37.548, -121.989),
            ("fresno", "ca", "Fresno", 36.738, -119.784),
            ("fullerton", "ca", "Fullerton", 33.870, -117.924),
            ("gardengrove", "ca", "Garden Grove", 33.774, -117.941),
            ("gilroy", "ca", "Gilroy", 37.006, -121.568),
            ("glendale", "ca", "Glendale", 34.142, -118.255),
            ("greakoaks", "ca", "Great Oaks", 37.238, -121.778),
            ("greatoaks", "ca", "Great Oaks", 37.238, -121.778),
            ("hayward", "ca", "Hayward", 37.669, -122.081),
            ("hemet", "ca", "Hemet", 33.748, -116.972),
            ("hesperia", "ca", "Hesperia", 34.426, -117.301),
            ("huntingtonbeach", "ca", "Huntington Beach", 33.660, -117.999),
            ("inglewood", "ca", "Inglewood", 33.962, -118.353),
            ("irvine", "ca", "Irvine", 33.684, -117.826),
            ("lancaster", "ca", "Lancaster", 34.698, -118.137),
            ("livermore", "ca", "Livermore", 37.682, -121.768),
            ("lodi", "ca", "Lodi", 38.130, -121.272),
            ("longbeach", "ca", "Long Beach", 33.770, -118.194),
            ("losangeles", "ca", "Los Angeles", 34.052, -118.244),
            ("manteca", "ca", "Manteca", 37.797, -121.216),
            ("menlopark", "ca", "Menlo Park", 37.454, -122.182),
            ("merced", "ca", "Merced", 37.302, -120.483),
            ("milpitas", "ca", "Milpitas", 37.432, -121.899),
            ("modesto", "ca", "Modesto", 37.639, -120.997),
            ("morenovalley", "ca", "Moreno Valley", 33.938, -117.231),
            ("morganhill", "ca", "Morgan Hill", 37.130, -121.654),
            ("mountainview", "ca", "Mountain View", 37.386, -122.084),
            ("murrieta", "ca", "Murrieta", 33.554, -117.213),
            ("napa", "ca", "Napa", 38.297, -122.286),
            ("newark", "ca", "Newark", 37.530, -122.040),
            ("newportbeach", "ca", "Newport Beach", 33.617, -117.929),
            ("oakland", "ca", "Oakland", 37.804, -122.271),
            ("oceanside", "ca", "Oceanside", 33.197, -117.380),
            ("ontario", "ca", "Ontario", 34.063, -117.651),
            ("orange", "ca", "Orange", 33.788, -117.853),
            ("oroville", "ca", "Oroville", 39.514, -121.556),
            ("oxnard", "ca", "Oxnard", 34.198, -119.177),
            ("palmdale", "ca", "Palmdale", 34.579, -118.117),
            ("palmsprings", "ca", "Palm Springs", 33.830, -116.545),
            ("paloalto", "ca", "Palo Alto", 37.442, -122.143),
            ("paradise", "ca", "Paradise", 39.760, -121.622),
            ("pasadena", "ca", "Pasadena", 34.156, -118.132),
            ("petaluma", "ca", "Petaluma", 38.233, -122.637),
            ("pleasanton", "ca", "Pleasanton", 37.663, -121.875),
            ("pomona", "ca", "Pomona", 34.055, -117.750),
            ("ranchocordova", "ca", "Rancho Cordova", 38.589, -121.303),
            ("ranchocucamonga", "ca", "Rancho Cucamonga", 34.106, -117.593),
            ("redbluff", "ca", "Red Bluff", 40.179, -122.236),
            ("redding", "ca", "Redding", 40.587, -122.392),
            ("redlands", "ca", "Redlands", 34.056, -117.183),
            ("redondoheach", "ca", "Redondo Beach", 33.849, -118.388),
            ("redwoodcity", "ca", "Redwood City", 37.487, -122.236),
            ("rialto", "ca", "Rialto", 34.106, -117.370),
            ("richmond", "ca", "Richmond", 37.936, -122.348),
            ("riverside", "ca", "Riverside", 33.981, -117.375),
            ("rocklin", "ca", "Rocklin", 38.791, -121.236),
            ("roseville", "ca", "Roseville", 38.752, -121.288),
            ("sacramento", "ca", "Sacramento", 38.582, -121.494),
            ("salinas", "ca", "Salinas", 36.677, -121.656),
            ("sanbernardino", "ca", "San Bernardino", 34.108, -117.290),
            ("sanbruno", "ca", "San Bruno", 37.630, -122.411),
            ("sancarlos", "ca", "San Carlos", 37.507, -122.260),
            ("sanclemente", "ca", "San Clemente", 33.427, -117.612),
            ("sandiego", "ca", "San Diego", 32.716, -117.161),
            ("sanfernando", "ca", "San Fernando", 34.282, -118.439),
            ("sanfrancisco", "ca", "San Francisco", 37.775, -122.418),
            ("sanjose", "ca", "San Jose", 37.339, -121.895),
            ("sanleandro", "ca", "San Leandro", 37.725, -122.156),
            ("sanluisobispo", "ca", "San Luis Obispo", 35.283, -120.660),
            ("sanmarcos", "ca", "San Marcos", 33.143, -117.166),
            ("sanmateo", "ca", "San Mateo", 37.563, -122.323),
            ("sanrafael", "ca", "San Rafael", 37.974, -122.531),
            ("sanramon", "ca", "San Ramon", 37.780, -121.978),
            ("santaana", "ca", "Santa Ana", 33.746, -117.868),
            ("santabarbara", "ca", "Santa Barbara", 34.421, -119.698),
            ("santaclara", "ca", "Santa Clara", 37.354, -121.955),
            ("santaclarita", "ca", "Santa Clarita", 34.392, -118.543),
            ("santacruz", "ca", "Santa Cruz", 36.974, -122.031),
            ("santamaria", "ca", "Santa Maria", 34.953, -120.436),
            ("santamonica", "ca", "Santa Monica", 34.019, -118.492),
            ("santarosa", "ca", "Santa Rosa", 38.440, -122.714),
            ("simivalley", "ca", "Simi Valley", 34.269, -118.781),
            ("southsanfrancisco", "ca", "South San Francisco", 37.655, -122.408),
            ("stockton", "ca", "Stockton", 37.958, -121.291),
            ("sunnyvale", "ca", "Sunnyvale", 37.369, -122.036),
            ("temecula", "ca", "Temecula", 33.494, -117.148),
            ("thousandoaks", "ca", "Thousand Oaks", 34.171, -118.838),
            ("torrance", "ca", "Torrance", 33.836, -118.341),
            ("tracy", "ca", "Tracy", 37.740, -121.425),
            ("tulare", "ca", "Tulare", 36.208, -119.348),
            ("turlock", "ca", "Turlock", 37.494, -120.847),
            ("tustin", "ca", "Tustin", 33.746, -117.826),
            ("unioncity", "ca", "Union City", 37.596, -122.044),
            ("upland", "ca", "Upland", 34.098, -117.648),
            ("vacaville", "ca", "Vacaville", 38.357, -121.988),
            ("vallejo", "ca", "Vallejo", 38.104, -122.257),
            ("victorville", "ca", "Victorville", 34.536, -117.292),
            ("visalia", "ca", "Visalia", 36.330, -119.292),
            ("walnutcreek", "ca", "Walnut Creek", 37.906, -122.065),
            ("watsonville", "ca", "Watsonville", 36.910, -121.757),
            ("westsacramento", "ca", "West Sacramento", 38.580, -121.530),
            ("woodland", "ca", "Woodland", 38.679, -121.773),
            ("yubacity", "ca", "Yuba City", 39.140, -121.617),
            // === COLORADO ===
            ("arvada", "co", "Arvada", 39.803, -105.087),
            ("auroura", "co", "Aurora", 39.729, -104.832),
            ("aurora", "co", "Aurora", 39.729, -104.832),
            ("boulder", "co", "Boulder", 40.015, -105.271),
            ("coloradosprings", "co", "Colorado Springs", 38.834, -104.821),
            ("denver", "co", "Denver", 39.739, -104.990),
            ("fortcollins", "co", "Fort Collins", 40.585, -105.084),
            ("lakewood", "co", "Lakewood", 39.705, -105.081),
            ("pueblo", "co", "Pueblo", 38.254, -104.609),
            // === CONNECTICUT ===
            ("bridgeport", "ct", "Bridgeport", 41.187, -73.195),
            ("hartford", "ct", "Hartford", 41.764, -72.682),
            ("newhaven", "ct", "New Haven", 41.308, -72.928),
            ("stamford", "ct", "Stamford", 41.053, -73.539),
            // === DC ===
            ("washington", "dc", "Washington", 38.907, -77.037),
            // === DELAWARE ===
            ("dover", "de", "Dover", 39.158, -75.524),
            ("wilmington", "de", "Wilmington", 39.746, -75.547),
            // === FLORIDA ===
            ("capecoral", "fl", "Cape Coral", 26.563, -81.950),
            ("clearwater", "fl", "Clearwater", 27.966, -82.800),
            ("coralsprings", "fl", "Coral Springs", 26.271, -80.271),
            ("davie", "fl", "Davie", 26.063, -80.233),
            ("fortlauderdale", "fl", "Fort Lauderdale", 26.122, -80.144),
            ("gainesville", "fl", "Gainesville", 29.652, -82.325),
            ("hialeah", "fl", "Hialeah", 25.858, -80.278),
            ("hollywood", "fl", "Hollywood", 26.011, -80.149),
            ("jacksonville", "fl", "Jacksonville", 30.332, -81.656),
            ("lakeland", "fl", "Lakeland", 28.040, -81.950),
            ("miami", "fl", "Miami", 25.762, -80.192),
            ("miamigardens", "fl", "Miami Gardens", 25.942, -80.246),
            ("naples", "fl", "Naples", 26.142, -81.795),
            ("orlando", "fl", "Orlando", 28.538, -81.379),
            ("pembrokepines", "fl", "Pembroke Pines", 26.013, -80.314),
            ("pensacola", "fl", "Pensacola", 30.421, -87.217),
            ("pompanobeach", "fl", "Pompano Beach", 26.238, -80.125),
            ("portstlucie", "fl", "Port St. Lucie", 27.274, -80.354),
            ("sarasota", "fl", "Sarasota", 27.336, -82.531),
            ("stpetersburg", "fl", "St. Petersburg", 27.773, -82.640),
            ("tallahassee", "fl", "Tallahassee", 30.438, -84.281),
            ("tampa", "fl", "Tampa", 27.951, -82.458),
            ("westpalmbeach", "fl", "West Palm Beach", 26.715, -80.054),
            // === GEORGIA ===
            ("atlanta", "ga", "Atlanta", 33.749, -84.388),
            ("augusta", "ga", "Augusta", 33.474, -81.975),
            ("columbus", "ga", "Columbus", 32.461, -84.988),
            ("macon", "ga", "Macon", 32.841, -83.632),
            ("savannah", "ga", "Savannah", 32.081, -81.091),
            // === HAWAII ===
            ("honolulu", "hi", "Honolulu", 21.307, -157.858),
            // === IDAHO ===
            ("boise", "id", "Boise", 43.615, -116.202),
            // === ILLINOIS ===
            ("aurora", "il", "Aurora", 41.761, -88.320),
            ("champaign", "il", "Champaign", 40.116, -88.243),
            ("chicago", "il", "Chicago", 41.878, -87.630),
            ("joliet", "il", "Joliet", 41.525, -88.082),
            ("naperville", "il", "Naperville", 41.786, -88.148),
            ("peoria", "il", "Peoria", 40.694, -89.589),
            ("rockford", "il", "Rockford", 42.271, -89.094),
            ("springfield", "il", "Springfield", 39.781, -89.650),
            // === INDIANA ===
            ("fortwayne", "in", "Fort Wayne", 41.079, -85.139),
            ("indianapolis", "in", "Indianapolis", 39.768, -86.158),
            ("southbend", "in", "South Bend", 41.677, -86.252),
            // === IOWA ===
            ("cedarrapids", "ia", "Cedar Rapids", 41.978, -91.665),
            ("davenport", "ia", "Davenport", 41.524, -90.577),
            ("desmoines", "ia", "Des Moines", 41.587, -93.621),
            // === KANSAS ===
            ("kansascity", "ks", "Kansas City", 39.114, -94.627),
            ("overland", "ks", "Overland Park", 38.982, -94.670),
            ("overlandpark", "ks", "Overland Park", 38.982, -94.670),
            ("topeka", "ks", "Topeka", 39.049, -95.678),
            ("wichita", "ks", "Wichita", 37.689, -97.336),
            // === KENTUCKY ===
            ("lexington", "ky", "Lexington", 38.040, -84.504),
            ("louisville", "ky", "Louisville", 38.253, -85.760),
            // === LOUISIANA ===
            ("batonrouge", "la", "Baton Rouge", 30.451, -91.187),
            ("neworleans", "la", "New Orleans", 29.951, -90.072),
            ("shreveport", "la", "Shreveport", 32.525, -93.750),
            // === MAINE ===
            ("portland", "me", "Portland", 43.661, -70.256),
            // === MARYLAND ===
            ("baltimore", "md", "Baltimore", 39.290, -76.612),
            // === MASSACHUSETTS ===
            ("boston", "ma", "Boston", 42.360, -71.059),
            ("cambridge", "ma", "Cambridge", 42.374, -71.110),
            ("lowell", "ma", "Lowell", 42.633, -71.316),
            ("springfield", "ma", "Springfield", 42.101, -72.590),
            ("worcester", "ma", "Worcester", 42.263, -71.802),
            // === MICHIGAN ===
            ("annarbor", "mi", "Ann Arbor", 42.281, -83.743),
            ("detroit", "mi", "Detroit", 42.331, -83.046),
            ("grandrapids", "mi", "Grand Rapids", 42.963, -85.668),
            ("lansing", "mi", "Lansing", 42.733, -84.556),
            // === MINNESOTA ===
            ("duluth", "mn", "Duluth", 46.787, -92.100),
            ("minneapolis", "mn", "Minneapolis", 44.978, -93.265),
            ("stpaul", "mn", "St. Paul", 44.944, -93.093),
            // === MISSISSIPPI ===
            ("jackson", "ms", "Jackson", 32.299, -90.185),
            // === MISSOURI ===
            ("kansascity", "mo", "Kansas City", 39.100, -94.578),
            ("springfield", "mo", "Springfield", 37.209, -93.292),
            ("stlouis", "mo", "St. Louis", 38.627, -90.199),
            // === MONTANA ===
            ("billings", "mt", "Billings", 45.784, -108.501),
            ("missoula", "mt", "Missoula", 46.872, -113.994),
            // === NEBRASKA ===
            ("lincoln", "ne", "Lincoln", 40.814, -96.702),
            ("omaha", "ne", "Omaha", 41.256, -95.934),
            // === NEVADA ===
            ("henderson", "nv", "Henderson", 36.040, -114.982),
            ("lasvegas", "nv", "Las Vegas", 36.169, -115.140),
            ("reno", "nv", "Reno", 39.530, -119.814),
            // === NEW HAMPSHIRE ===
            ("manchester", "nh", "Manchester", 42.996, -71.455),
            ("nashua", "nh", "Nashua", 42.766, -71.468),
            // === NEW JERSEY ===
            ("edison", "nj", "Edison", 40.519, -74.412),
            ("jerseycity", "nj", "Jersey City", 40.728, -74.078),
            ("newark", "nj", "Newark", 40.736, -74.172),
            ("paterson", "nj", "Paterson", 40.917, -74.172),
            ("trenton", "nj", "Trenton", 40.217, -74.743),
            // === NEW MEXICO ===
            ("albuquerque", "nm", "Albuquerque", 35.084, -106.651),
            ("lascruces", "nm", "Las Cruces", 32.350, -106.760),
            ("santafe", "nm", "Santa Fe", 35.687, -105.938),
            // === NEW YORK ===
            ("albany", "ny", "Albany", 42.653, -73.757),
            ("buffalo", "ny", "Buffalo", 42.887, -78.879),
            ("newyork", "ny", "New York", 40.713, -74.006),
            ("rochester", "ny", "Rochester", 43.157, -77.615),
            ("syracuse", "ny", "Syracuse", 43.049, -76.148),
            ("yonkers", "ny", "Yonkers", 40.931, -73.899),
            // === NORTH CAROLINA ===
            ("asheville", "nc", "Asheville", 35.595, -82.551),
            ("charlotte", "nc", "Charlotte", 35.227, -80.843),
            ("durham", "nc", "Durham", 35.994, -78.899),
            ("greensboro", "nc", "Greensboro", 36.073, -79.792),
            ("raleigh", "nc", "Raleigh", 35.780, -78.639),
            ("wilmington", "nc", "Wilmington", 34.226, -77.945),
            ("winstonsalem", "nc", "Winston-Salem", 36.100, -80.244),
            // === NORTH DAKOTA ===
            ("bismarck", "nd", "Bismarck", 46.809, -100.784),
            ("fargo", "nd", "Fargo", 46.877, -96.790),
            // === OHIO ===
            ("akron", "oh", "Akron", 41.081, -81.519),
            ("cincinnati", "oh", "Cincinnati", 39.100, -84.512),
            ("cleveland", "oh", "Cleveland", 41.499, -81.694),
            ("columbus", "oh", "Columbus", 39.962, -82.999),
            ("dayton", "oh", "Dayton", 39.759, -84.192),
            ("toledo", "oh", "Toledo", 41.654, -83.536),
            // === OKLAHOMA ===
            ("norman", "ok", "Norman", 35.222, -97.439),
            ("oklahomacity", "ok", "Oklahoma City", 35.468, -97.516),
            ("tulsa", "ok", "Tulsa", 36.154, -95.993),
            // === OREGON ===
            ("bend", "or", "Bend", 44.058, -121.315),
            ("corvallis", "or", "Corvallis", 44.565, -123.261),
            ("eugene", "or", "Eugene", 44.052, -123.087),
            ("medford", "or", "Medford", 42.326, -122.875),
            ("portland", "or", "Portland", 45.505, -122.675),
            ("salem", "or", "Salem", 44.943, -123.035),
            // === PENNSYLVANIA ===
            ("allentown", "pa", "Allentown", 40.608, -75.490),
            ("erie", "pa", "Erie", 42.129, -80.085),
            ("harrisburg", "pa", "Harrisburg", 40.264, -76.884),
            ("philadelphia", "pa", "Philadelphia", 39.953, -75.164),
            ("pittsburgh", "pa", "Pittsburgh", 40.441, -79.996),
            ("scranton", "pa", "Scranton", 41.409, -75.662),
            // === RHODE ISLAND ===
            ("providence", "ri", "Providence", 41.824, -71.413),
            // === SOUTH CAROLINA ===
            ("charleston", "sc", "Charleston", 32.776, -79.931),
            ("columbia", "sc", "Columbia", 34.000, -81.035),
            ("greenville", "sc", "Greenville", 34.852, -82.394),
            // === SOUTH DAKOTA ===
            ("rapidcity", "sd", "Rapid City", 44.081, -103.231),
            ("siouxfalls", "sd", "Sioux Falls", 43.550, -96.701),
            // === TENNESSEE ===
            ("chattanooga", "tn", "Chattanooga", 35.046, -85.310),
            ("knoxville", "tn", "Knoxville", 35.961, -83.921),
            ("memphis", "tn", "Memphis", 35.150, -90.049),
            ("nashville", "tn", "Nashville", 36.163, -86.781),
            // === TEXAS ===
            ("amarillo", "tx", "Amarillo", 35.222, -101.831),
            ("arlington", "tx", "Arlington", 32.736, -97.108),
            ("austin", "tx", "Austin", 30.267, -97.743),
            ("beaumont", "tx", "Beaumont", 30.080, -94.102),
            ("brownsville", "tx", "Brownsville", 25.932, -97.484),
            ("corpuschristi", "tx", "Corpus Christi", 27.801, -97.396),
            ("dallas", "tx", "Dallas", 32.777, -96.797),
            ("elpaso", "tx", "El Paso", 31.759, -106.487),
            ("fortworth", "tx", "Fort Worth", 32.755, -97.331),
            ("frisco", "tx", "Frisco", 33.151, -96.824),
            ("garland", "tx", "Garland", 32.913, -96.639),
            ("houston", "tx", "Houston", 29.760, -95.370),
            ("irving", "tx", "Irving", 32.814, -96.949),
            ("killeen", "tx", "Killeen", 31.117, -97.728),
            ("laredo", "tx", "Laredo", 27.507, -99.508),
            ("lubbock", "tx", "Lubbock", 33.578, -101.845),
            ("mcallen", "tx", "McAllen", 26.204, -98.230),
            ("mckinney", "tx", "McKinney", 33.198, -96.615),
            ("midland", "tx", "Midland", 31.997, -102.078),
            ("plano", "tx", "Plano", 33.020, -96.699),
            ("richardson", "tx", "Richardson", 32.948, -96.730),
            ("roundrock", "tx", "Round Rock", 30.508, -97.679),
            ("sanantonio", "tx", "San Antonio", 29.425, -98.495),
            ("sanmarcos", "tx", "San Marcos", 29.883, -97.941),
            ("waco", "tx", "Waco", 31.549, -97.146),
            // === UTAH ===
            ("ogden", "ut", "Ogden", 41.223, -111.974),
            ("orem", "ut", "Orem", 40.297, -111.695),
            ("provo", "ut", "Provo", 40.234, -111.659),
            ("saltlakecity", "ut", "Salt Lake City", 40.761, -111.891),
            ("sandy", "ut", "Sandy", 40.577, -111.884),
            ("stgeorge", "ut", "St. George", 37.096, -113.568),
            ("westjordan", "ut", "West Jordan", 40.610, -111.939),
            // === VERMONT ===
            ("burlington", "vt", "Burlington", 44.476, -73.213),
            // === VIRGINIA ===
            ("alexandria", "va", "Alexandria", 38.805, -77.047),
            ("arlington", "va", "Arlington", 38.880, -77.107),
            ("ashburn", "va", "Ashburn", 39.044, -77.487),
            ("chesapeake", "va", "Chesapeake", 36.768, -76.287),
            ("norfolk", "va", "Norfolk", 36.851, -76.286),
            ("richmond", "va", "Richmond", 37.541, -77.434),
            ("roanoke", "va", "Roanoke", 37.271, -79.942),
            ("virginiabeach", "va", "Virginia Beach", 36.853, -75.978),
            // === WASHINGTON ===
            ("bellevue", "wa", "Bellevue", 47.610, -122.201),
            ("everett", "wa", "Everett", 47.979, -122.202),
            ("kent", "wa", "Kent", 47.381, -122.235),
            ("olympia", "wa", "Olympia", 47.038, -122.899),
            ("seattle", "wa", "Seattle", 47.606, -122.332),
            ("spokane", "wa", "Spokane", 47.659, -117.426),
            ("tacoma", "wa", "Tacoma", 47.253, -122.444),
            ("vancouver", "wa", "Vancouver", 45.639, -122.661),
            // === WEST VIRGINIA ===
            ("charleston", "wv", "Charleston", 38.350, -81.633),
            // === WISCONSIN ===
            ("greenbay", "wi", "Green Bay", 44.513, -88.016),
            ("madison", "wi", "Madison", 43.073, -89.401),
            ("milwaukee", "wi", "Milwaukee", 43.039, -87.907),
            // === WYOMING ===
            ("casper", "wy", "Casper", 42.867, -106.313),
            ("cheyenne", "wy", "Cheyenne", 41.140, -104.820),
        ];

        foreach (var (key, state, name, lat, lon) in data)
            dict[(key, state)] = (name, lat, lon);

        return dict;
    }

    // CLLI 4-letter city codes (US telco standard)
    private static readonly Dictionary<string, (string City, double Lat, double Lon)> ClliCodes =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["atln"] = ("Atlanta", 33.749, -84.388),
        ["atlx"] = ("Atlanta", 33.749, -84.388),
        ["ausn"] = ("Austin", 30.267, -97.743),
        ["bflo"] = ("Buffalo", 42.887, -78.879),
        ["bltm"] = ("Baltimore", 39.290, -76.612),
        ["bstn"] = ("Boston", 42.360, -71.059),
        ["chcg"] = ("Chicago", 41.878, -87.630),
        ["chco"] = ("Chico", 39.729, -121.837),
        ["chrl"] = ("Charlotte", 35.227, -80.843),
        ["cinc"] = ("Cincinnati", 39.100, -84.512),
        ["clev"] = ("Cleveland", 41.499, -81.694),
        ["clmb"] = ("Columbus", 39.962, -82.999),
        ["denv"] = ("Denver", 39.739, -104.990),
        ["dlls"] = ("Dallas", 32.777, -96.797),
        ["dllx"] = ("Dallas", 32.777, -96.797),
        ["dtrt"] = ("Detroit", 42.331, -83.046),
        ["frno"] = ("Fresno", 36.738, -119.784),
        ["frsn"] = ("Fresno", 36.738, -119.784),
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
        ["sacr"] = ("Sacramento", 38.582, -121.494),
        ["scrm"] = ("Sacramento", 38.582, -121.494),
        ["sant"] = ("San Antonio", 29.425, -98.495),
        ["slkc"] = ("Salt Lake City", 40.761, -111.891),
        ["sndg"] = ("San Diego", 32.716, -117.161),
        ["snfc"] = ("San Francisco", 37.775, -122.418),
        ["snjs"] = ("San Jose", 37.339, -121.895),
        ["snjx"] = ("San Jose", 37.339, -121.895),
        ["snvl"] = ("Sunnyvale", 37.369, -122.036),
        ["stls"] = ("St. Louis", 38.627, -90.199),
        ["sttl"] = ("Seattle", 47.606, -122.332),
        ["tamp"] = ("Tampa", 27.951, -82.458),
        ["wash"] = ("Washington", 38.907, -77.037),
    };

    // Standalone city names for segment matching without state code (Level3/Lumen style)
    private static readonly (string Name, ParsedLocation Loc)[] StandaloneCityNames =
    [
        // Multi-word (longer first)
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
        // Single-word (unambiguous major cities)
        ("greatoaks", new("Great Oaks", "CA", "United States", 37.238, -121.778)),
        ("greakoaks", new("Great Oaks", "CA", "United States", 37.238, -121.778)),
        ("atlanta", new("Atlanta", "GA", "United States", 33.749, -84.388)),
        ("chicago", new("Chicago", "IL", "United States", 41.878, -87.630)),
        ("dallas", new("Dallas", "TX", "United States", 32.777, -96.797)),
        ("denver", new("Denver", "CO", "United States", 39.739, -104.990)),
        ("houston", new("Houston", "TX", "United States", 29.760, -95.370)),
        ("miami", new("Miami", "FL", "United States", 25.762, -80.192)),
        ("phoenix", new("Phoenix", "AZ", "United States", 33.449, -112.074)),
        ("seattle", new("Seattle", "WA", "United States", 47.606, -122.332)),
        // International
        ("amsterdam", new("Amsterdam", "", "Netherlands", 52.370, 4.895)),
        ("frankfurt", new("Frankfurt", "", "Germany", 50.110, 8.682)),
        ("london", new("London", "", "United Kingdom", 51.507, -0.128)),
        ("paris", new("Paris", "", "France", 48.857, 2.352)),
        ("singapore", new("Singapore", "", "Singapore", 1.352, 103.820)),
        ("tokyo", new("Tokyo", "", "Japan", 35.682, 139.692)),
        ("toronto", new("Toronto", "", "Canada", 43.653, -79.383)),
    ];

    // IATA 3-letter codes
    private static readonly Dictionary<string, ParsedLocation> IataCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // US
        ["atl"] = new("Atlanta", "GA", "United States", 33.749, -84.388),
        ["aus"] = new("Austin", "TX", "United States", 30.267, -97.743),
        ["bna"] = new("Nashville", "TN", "United States", 36.163, -86.781),
        ["bos"] = new("Boston", "MA", "United States", 42.360, -71.059),
        ["bwi"] = new("Baltimore", "MD", "United States", 39.290, -76.612),
        ["chi"] = new("Chicago", "IL", "United States", 41.878, -87.630),
        ["cle"] = new("Cleveland", "OH", "United States", 41.499, -81.694),
        ["clt"] = new("Charlotte", "NC", "United States", 35.227, -80.843),
        ["cmh"] = new("Columbus", "OH", "United States", 39.962, -82.999),
        ["cvg"] = new("Cincinnati", "OH", "United States", 39.100, -84.512),
        ["dal"] = new("Dallas", "TX", "United States", 32.777, -96.797),
        ["dca"] = new("Washington", "DC", "United States", 38.907, -77.037),
        ["den"] = new("Denver", "CO", "United States", 39.739, -104.990),
        ["dfw"] = new("Dallas", "TX", "United States", 32.777, -96.797),
        ["dtw"] = new("Detroit", "MI", "United States", 42.331, -83.046),
        ["ewr"] = new("Newark", "NJ", "United States", 40.736, -74.172),
        ["hou"] = new("Houston", "TX", "United States", 29.760, -95.370),
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
        ["nyc"] = new("New York", "NY", "United States", 40.713, -74.006),
        ["oma"] = new("Omaha", "NE", "United States", 41.256, -95.934),
        ["ord"] = new("Chicago", "IL", "United States", 41.878, -87.630),
        ["pdx"] = new("Portland", "OR", "United States", 45.505, -122.675),
        ["phl"] = new("Philadelphia", "PA", "United States", 39.953, -75.164),
        ["phx"] = new("Phoenix", "AZ", "United States", 33.449, -112.074),
        ["pit"] = new("Pittsburgh", "PA", "United States", 40.441, -79.996),
        ["rdu"] = new("Raleigh", "NC", "United States", 35.780, -78.639),
        ["sat"] = new("San Antonio", "TX", "United States", 29.425, -98.495),
        ["sea"] = new("Seattle", "WA", "United States", 47.606, -122.332),
        ["sfo"] = new("San Francisco", "CA", "United States", 37.775, -122.418),
        ["sjc"] = new("San Jose", "CA", "United States", 37.339, -121.895),
        ["slc"] = new("Salt Lake City", "UT", "United States", 40.761, -111.891),
        ["stl"] = new("St. Louis", "MO", "United States", 38.627, -90.199),
        ["tpa"] = new("Tampa", "FL", "United States", 27.951, -82.458),
        ["was"] = new("Washington", "DC", "United States", 38.907, -77.037),
        // International
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

    private static readonly HashSet<string> UsStates = new(StringComparer.OrdinalIgnoreCase)
    {
        "al","ak","az","ar","ca","co","ct","dc","de","fl","ga","hi","id","il","in",
        "ia","ks","ky","la","me","md","ma","mi","mn","ms","mo","mt","ne","nv","nh",
        "nj","nm","ny","nc","nd","oh","ok","or","pa","ri","sc","sd","tn","tx","ut",
        "vt","va","wa","wv","wi","wy"
    };
}
