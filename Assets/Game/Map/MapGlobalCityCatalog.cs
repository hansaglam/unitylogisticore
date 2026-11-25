using System;
using System.Collections.Generic;

namespace LogistiCore.Map
{
    /// <summary>Verified cartographic place; this is not automatically an active contract destination.</summary>
    public readonly struct MapGlobalCity
    {
        public readonly MapBusinessLocation Location;
        public readonly string DisplayName;
        public readonly double Latitude;
        public readonly double Longitude;
        public readonly int ScaleRank;

        public MapGlobalCity(string countryCode, string cityKey, string displayName,
            double latitude, double longitude, int scaleRank)
        {
            Location = new MapBusinessLocation(countryCode, cityKey);
            DisplayName = displayName ?? string.Empty;
            Latitude = latitude;
            Longitude = longitude;
            ScaleRank = scaleRank;
        }
    }

    /// <summary>
    /// Initial worldwide settlement seed from Natural Earth ne_110m_populated_places,
    /// master, fetched 2026-11-26 (public domain). Original WIKIDATAID is retained
    /// as ne-qNNN key. Eight established Turkish markers keep their legacy ids and
    /// their 10m marker coordinates. All other source records are unmodified points.
    /// Source: https://github.com/nvkelso/natural-earth-vector/blob/master/geojson/ne_110m_populated_places.geojson
    /// This is an initial tier of 249 places; full cities15000/10m dataset
    /// import and map marker LOD are follow-up tasks. NEVER infer roads or delivery
    /// permissions from this list.
    /// </summary>
    public static class MapGlobalCityCatalog
    {
        public static readonly MapGlobalCity[] All =
        {
            new MapGlobalCity("AFG", "ne-q5838", "Kabul", 34.518636d, 69.181314d, 1),
            new MapGlobalCity("AGO", "ne-q3897", "Luanda", -8.836340d, 13.232481d, 2),
            new MapGlobalCity("ALB", "ne-q19689", "Tirana", 41.327541d, 19.818883d, 3),
            new MapGlobalCity("AND", "ne-q1863", "Andorra", 42.500001d, 1.516486d, 6),
            new MapGlobalCity("ARE", "ne-q1519", "Abu Dhabi", 24.466684d, 54.366593d, 3),
            new MapGlobalCity("ARE", "ne-q612", "Dubai", 25.231942d, 55.278029d, 1),
            new MapGlobalCity("ARG", "ne-q1486", "Buenos Aires", -34.600556d, -58.399477d, 1),
            new MapGlobalCity("ARM", "ne-q1953", "Yerevan", 40.183097d, 44.511606d, 3),
            new MapGlobalCity("ATG", "ne-q36262", "Saint John's", 17.118037d, -61.850034d, 4),
            new MapGlobalCity("AUS", "ne-q3114", "Canberra", -35.283029d, 149.129026d, 3),
            new MapGlobalCity("AUS", "ne-q3130", "Sydney", -33.918065d, 151.183234d, 0),
            new MapGlobalCity("AUS", "ne-q3141", "Melbourne", -37.818086d, 144.973070d, 1),
            new MapGlobalCity("AUT", "ne-q1741", "Vienna", 48.201961d, 16.364693d, 1),
            new MapGlobalCity("AZE", "ne-q9248", "Baku", 40.397218d, 49.860271d, 3),
            new MapGlobalCity("BDI", "ne-q3854", "Bujumbura", -3.376087d, 29.360006d, 4),
            new MapGlobalCity("BEL", "ne-q239", "Brussels", 50.835263d, 4.331371d, 2),
            new MapGlobalCity("BEN", "ne-q3799", "Porto-Novo", 6.483311d, 2.616626d, 4),
            new MapGlobalCity("BEN", "ne-q43595", "Cotonou", 6.401954d, 2.518045d, 3),
            new MapGlobalCity("BFA", "ne-q3777", "Ouagadougou", 12.372262d, -1.526670d, 3),
            new MapGlobalCity("BGD", "ne-q1354", "Dhaka", 23.725006d, 90.406634d, 2),
            new MapGlobalCity("BGR", "ne-q472", "Sofia", 42.685295d, 23.314708d, 3),
            new MapGlobalCity("BHR", "ne-q3882", "Manama", 26.236136d, 50.583052d, 4),
            new MapGlobalCity("BHS", "ne-q2467", "Nassau", 25.083390d, -77.350044d, 3),
            new MapGlobalCity("BIH", "ne-q11194", "Sarajevo", 43.850022d, 18.383002d, 3),
            new MapGlobalCity("BLR", "ne-q2280", "Minsk", 53.901923d, 27.564681d, 3),
            new MapGlobalCity("BLZ", "ne-q3043", "Belmopan", 17.252034d, -88.767073d, 3),
            new MapGlobalCity("BOL", "ne-q1491", "La Paz", -16.496028d, -68.151931d, 3),
            new MapGlobalCity("BOL", "ne-q2907", "Sucre", -19.040971d, -65.259516d, 3),
            new MapGlobalCity("BRA", "ne-q174", "São Paulo", -23.556734d, -46.626966d, 0),
            new MapGlobalCity("BRA", "ne-q2844", "Brasília", -15.781394d, -47.917998d, 2),
            new MapGlobalCity("BRA", "ne-q8678", "Rio de Janeiro", -22.923077d, -43.226967d, 0),
            new MapGlobalCity("BRB", "ne-q36168", "Bridgetown", 13.102003d, -59.616527d, 4),
            new MapGlobalCity("BRN", "ne-q9279", "Bandar Seri Begawan", 4.883331d, 114.933284d, 3),
            new MapGlobalCity("BTN", "ne-q9270", "Thimphu", 27.472986d, 89.639014d, 3),
            new MapGlobalCity("BWA", "ne-q3919", "Gaborone", -24.646314d, 25.911948d, 3),
            new MapGlobalCity("CAF", "ne-q3832", "Bangui", 4.366644d, 18.558288d, 3),
            new MapGlobalCity("CAN", "ne-q172", "Toronto", 43.701926d, -79.421967d, 1),
            new MapGlobalCity("CAN", "ne-q1930", "Ottawa", 45.418643d, -75.701961d, 3),
            new MapGlobalCity("CAN", "ne-q24639", "Vancouver", 49.275362d, -123.123590d, 1),
            new MapGlobalCity("CHE", "ne-q70", "Bern", 46.916683d, 7.466976d, 4),
            new MapGlobalCity("CHE", "ne-q71", "Geneva", 46.210008d, 6.140028d, 1),
            new MapGlobalCity("CHL", "ne-q2887", "Santiago", -33.448068d, -70.668987d, 0),
            new MapGlobalCity("CHL", "ne-q33986", "Valparaíso", -33.045819d, -71.622960d, 3),
            new MapGlobalCity("CHN", "ne-q16959", "Ürümqi", 43.806958d, 87.573060d, 1),
            new MapGlobalCity("CHN", "ne-q30002", "Chengdu", 30.671946d, 104.068074d, 1),
            new MapGlobalCity("CHN", "ne-q8686", "Shanghai", 31.218398d, 121.434559d, 0),
            new MapGlobalCity("CHN", "ne-q956", "Beijing", 39.930838d, 116.386340d, 0),
            new MapGlobalCity("CIV", "ne-q1515", "Abidjan", 5.321943d, -4.041994d, 2),
            new MapGlobalCity("CIV", "ne-q3768", "Yamoussoukro", 6.818381d, -5.275503d, 3),
            new MapGlobalCity("CMR", "ne-q3808", "Yaoundé", 3.868647d, 11.514705d, 3),
            new MapGlobalCity("COD", "ne-q3838", "Kinshasa", -4.327778d, 15.313026d, 1),
            new MapGlobalCity("COG", "ne-q3844", "Brazzaville", -4.257240d, 15.282744d, 3),
            new MapGlobalCity("COL", "ne-q2841", "Bogota", 4.598369d, -74.085290d, 0),
            new MapGlobalCity("COM", "ne-q3901", "Moroni", -11.704158d, 43.240244d, 6),
            new MapGlobalCity("CPV", "ne-q3751", "Praia", 14.916698d, -23.516689d, 3),
            new MapGlobalCity("CRI", "ne-q3070", "San José", 9.936958d, -84.085997d, 3),
            new MapGlobalCity("CUB", "ne-q1563", "Havana", 23.133905d, -82.366128d, 2),
            new MapGlobalCity("CYP", "ne-q3856", "Nicosia", 35.166677d, 33.366635d, 3),
            new MapGlobalCity("CZE", "ne-q1085", "Prague", 50.085283d, 14.464034d, 2),
            new MapGlobalCity("DEU", "ne-q64", "Berlin", 52.523765d, 13.399603d, 1),
            new MapGlobalCity("DJI", "ne-q3604", "Djibouti", 11.595015d, 43.148002d, 4),
            new MapGlobalCity("DMA", "ne-q36281", "Roseau", 15.301016d, -61.387013d, 4),
            new MapGlobalCity("DNK", "ne-q1748", "København", 55.680510d, 12.561540d, 2),
            new MapGlobalCity("DOM", "ne-q34820", "Santo Domingo", 18.472019d, -69.902031d, 2),
            new MapGlobalCity("DZA", "ne-q3561", "Algiers", 36.765011d, 3.048607d, 2),
            new MapGlobalCity("ECU", "ne-q2900", "Quito", -0.213042d, -78.501997d, 3),
            new MapGlobalCity("EGY", "ne-q85", "Cairo", 30.051906d, 31.248022d, 0),
            new MapGlobalCity("ERI", "ne-q3642", "Asmara", 15.333339d, 38.933324d, 3),
            new MapGlobalCity("ESP", "ne-q2807", "Madrid", 40.401972d, -3.685298d, 1),
            new MapGlobalCity("EST", "ne-q1770", "Tallinn", 59.433877d, 24.728041d, 3),
            new MapGlobalCity("ETH", "ne-q3624", "Addis Ababa", 9.035256d, 38.698059d, 1),
            new MapGlobalCity("FIN", "ne-q1757", "Helsinki", 60.177509d, 24.932181d, 2),
            new MapGlobalCity("FJI", "ne-q38807", "Suva", -18.133016d, 178.441707d, 3),
            new MapGlobalCity("FRA", "ne-q90", "Paris", 48.868639d, 2.331390d, 0),
            new MapGlobalCity("FSM", "ne-q42751", "Palikir", 6.916644d, 158.149974d, 6),
            new MapGlobalCity("GAB", "ne-q3825", "Libreville", 0.385389d, 9.457965d, 3),
            new MapGlobalCity("GBR", "ne-q84", "London", 51.501941d, -0.118668d, 0),
            new MapGlobalCity("GEO", "ne-q994", "Tbilisi", 41.726956d, 44.788850d, 3),
            new MapGlobalCity("GHA", "ne-q3761", "Accra", 5.551981d, -0.218662d, 2),
            new MapGlobalCity("GIN", "ne-q3733", "Conakry", 9.533469d, -13.682181d, 3),
            new MapGlobalCity("GMB", "ne-q3726", "Banjul", 13.453877d, -16.591702d, 4),
            new MapGlobalCity("GNB", "ne-q3739", "Bissau", 11.865024d, -15.598361d, 3),
            new MapGlobalCity("GNQ", "ne-q3818", "Malabo", 3.750015d, 8.783278d, 3),
            new MapGlobalCity("GRC", "ne-q1524", "Athens", 37.985272d, 23.731375d, 1),
            new MapGlobalCity("GRD", "ne-q41547", "Saint George's", 12.052633d, -61.741643d, 4),
            new MapGlobalCity("GTM", "ne-q1555", "Guatemala City", 14.623081d, -90.528911d, 3),
            new MapGlobalCity("GUY", "ne-q10717", "Georgetown", 6.801974d, -58.167029d, 3),
            new MapGlobalCity("HKG", "ne-q8646", "Hong Kong", 22.306927d, 114.183064d, 0),
            new MapGlobalCity("HND", "ne-q3238", "Tegucigalpa", 14.103991d, -87.219475d, 3),
            new MapGlobalCity("HRV", "ne-q1435", "Zagreb", 45.800007d, 15.999995d, 3),
            new MapGlobalCity("HTI", "ne-q34261", "Port-au-Prince", 18.542971d, -72.337980d, 3),
            new MapGlobalCity("HUN", "ne-q1781", "Budapest", 47.501952d, 19.081375d, 2),
            new MapGlobalCity("IDN", "ne-q3630", "Jakarta", -6.172472d, 106.827492d, 0),
            new MapGlobalCity("IND", "ne-q1156", "Mumbai", 19.018936d, 72.855043d, 0),
            new MapGlobalCity("IND", "ne-q1348", "Kolkata", 22.496915d, 88.322730d, 0),
            new MapGlobalCity("IND", "ne-q1355", "Bengaluru", 12.971941d, 77.558064d, 1),
            new MapGlobalCity("IND", "ne-q987", "New Delhi", 28.600023d, 77.199980d, 1),
            new MapGlobalCity("IRL", "ne-q1761", "Dublin", 53.335007d, -6.250852d, 2),
            new MapGlobalCity("IRN", "ne-q3616", "Tehran", 35.673889d, 51.422398d, 1),
            new MapGlobalCity("IRQ", "ne-q1530", "Baghdad", 33.340594d, 44.391923d, 1),
            new MapGlobalCity("ISL", "ne-q1764", "Reykjavík", 64.150024d, -21.950015d, 3),
            new MapGlobalCity("ISR", "ne-q1218", "Jerusalem", 31.778408d, 35.206626d, 3),
            new MapGlobalCity("ISR", "ne-q33935", "Tel Aviv", 32.081937d, 34.768066d, 2),
            new MapGlobalCity("ITA", "ne-q220", "Rome", 41.897902d, 12.481313d, 0),
            new MapGlobalCity("JAM", "ne-q34692", "Kingston", 17.977077d, -76.767434d, 3),
            new MapGlobalCity("JOR", "ne-q3805", "Amman", 31.951971d, 35.931354d, 3),
            new MapGlobalCity("JPN", "ne-q1490", "Tokyo", 35.686963d, 139.749462d, 0),
            new MapGlobalCity("JPN", "ne-q34600", "Kyoto", 35.031938d, 135.748052d, 4),
            new MapGlobalCity("JPN", "ne-q35765", "Ōsaka", 34.751981d, 135.458199d, 1),
            new MapGlobalCity("KAZ", "ne-q1520", "Nur-Sultan", 51.181125d, 71.427774d, 3),
            new MapGlobalCity("KEN", "ne-q3870", "Nairobi", -1.281401d, 36.814711d, 0),
            new MapGlobalCity("KGZ", "ne-q9361", "Bishkek", 42.875025d, 74.583258d, 3),
            new MapGlobalCity("KHM", "ne-q1850", "Phnom Penh", 11.551976d, 104.914689d, 3),
            new MapGlobalCity("KIR", "ne-q131233", "Tarawa", 1.338188d, 173.017571d, 6),
            new MapGlobalCity("KNA", "ne-q41295", "Basseterre", 17.302031d, -62.717009d, 4),
            new MapGlobalCity("KOR", "ne-q8684", "Seoul", 37.568295d, 126.997785d, 1),
            new MapGlobalCity("KOS", "ne-q25270", "Pristina", 42.666710d, 21.165984d, 4),
            new MapGlobalCity("KWT", "ne-q35178", "Kuwait City", 29.371664d, 47.976355d, 2),
            new MapGlobalCity("LAO", "ne-q9326", "Vientiane", 17.966693d, 102.599980d, 3),
            new MapGlobalCity("LBN", "ne-q3820", "Beirut", 33.873921d, 35.507762d, 3),
            new MapGlobalCity("LBR", "ne-q3748", "Monrovia", 6.314582d, -10.799660d, 2),
            new MapGlobalCity("LBY", "ne-q3579", "Tripoli", 32.892500d, 13.180012d, 2),
            new MapGlobalCity("LCA", "ne-q41699", "Castries", 14.001974d, -61.000008d, 4),
            new MapGlobalCity("LIE", "ne-q1844", "Vaduz", 47.133724d, 9.516670d, 7),
            new MapGlobalCity("LKA", "ne-q35381", "Colombo", 6.931966d, 79.857751d, 3),
            new MapGlobalCity("LKA", "ne-q41963", "Sri Jayawardenepura Kotte", 6.900004d, 79.949993d, 4),
            new MapGlobalCity("LSO", "ne-q3909", "Maseru", -29.316674d, 27.483273d, 3),
            new MapGlobalCity("LTU", "ne-q216", "Vilnius", 54.683366d, 25.316635d, 3),
            new MapGlobalCity("LUX", "ne-q1842", "Luxembourg", 49.611660d, 6.130003d, 6),
            new MapGlobalCity("LVA", "ne-q1773", "Riga", 56.950024d, 24.099965d, 3),
            new MapGlobalCity("MAR", "ne-q3551", "Rabat", 34.025307d, -6.836408d, 3),
            new MapGlobalCity("MAR", "ne-q47837", "Laayoune", 27.149982d, -13.200006d, 4),
            new MapGlobalCity("MAR", "ne-q7903", "Casablanca", 33.601922d, -7.618313d, 1),
            new MapGlobalCity("MCO", "ne-q235", "Monaco", 43.739646d, 7.406913d, 6),
            new MapGlobalCity("MDA", "ne-q21197", "Chișinău", 47.005024d, 28.857711d, 3),
            new MapGlobalCity("MDG", "ne-q3915", "Antananarivo", -18.914692d, 47.514678d, 3),
            new MapGlobalCity("MDV", "ne-q9347", "Malé", 4.166708d, 73.499948d, 3),
            new MapGlobalCity("MEX", "ne-q1489", "Mexico City", 19.444388d, -99.132934d, 0),
            new MapGlobalCity("MEX", "ne-q81033", "Monterrey", 25.671941d, -100.331931d, 1),
            new MapGlobalCity("MHL", "ne-q12919", "Majuro", 7.103004d, 171.380000d, 6),
            new MapGlobalCity("MKD", "ne-q384", "Skopje", 42.000006d, 21.433462d, 4),
            new MapGlobalCity("MLI", "ne-q3703", "Bamako", 12.651961d, -8.001985d, 3),
            new MapGlobalCity("MLT", "ne-q23800", "Valletta", 35.899733d, 14.514711d, 3),
            new MapGlobalCity("MMR", "ne-q37400", "Naypyidaw", 19.768503d, 96.116673d, 3),
            new MapGlobalCity("MMR", "ne-q37995", "Yangon", 16.785300d, 96.164732d, 2),
            new MapGlobalCity("MNE", "ne-q23564", "Podgorica", 42.465973d, 19.266307d, 4),
            new MapGlobalCity("MNG", "ne-q23430", "Ulaanbaatar", 47.918619d, 106.914670d, 3),
            new MapGlobalCity("MOZ", "ne-q3889", "Maputo", -25.953332d, 32.587217d, 3),
            new MapGlobalCity("MRT", "ne-q3688", "Nouakchott", 18.086427d, -15.975340d, 3),
            new MapGlobalCity("MUS", "ne-q3929", "Port Louis", -20.166639d, 57.499994d, 4),
            new MapGlobalCity("MWI", "ne-q3876", "Lilongwe", -13.983295d, 33.783302d, 3),
            new MapGlobalCity("MYS", "ne-q182378", "Putrajaya", 2.914020d, 101.701947d, 4),
            new MapGlobalCity("MYS", "ne-q1865", "Kuala Lumpur", 3.168612d, 101.698037d, 2),
            new MapGlobalCity("NAM", "ne-q3935", "Windhoek", -22.570006d, 17.083546d, 3),
            new MapGlobalCity("NER", "ne-q3674", "Niamey", 13.518652d, 2.114710d, 3),
            new MapGlobalCity("NGA", "ne-q3787", "Abuja", 9.085279d, 7.531382d, 3),
            new MapGlobalCity("NGA", "ne-q8673", "Lagos", 6.445208d, 3.389585d, 0),
            new MapGlobalCity("NIC", "ne-q3274", "Managua", 12.154962d, -86.270438d, 3),
            new MapGlobalCity("NLD", "ne-q36600", "The Hague", 52.080037d, 4.269961d, 4),
            new MapGlobalCity("NLD", "ne-q727", "Amsterdam", 52.351915d, 4.914694d, 1),
            new MapGlobalCity("NOR", "ne-q585", "Oslo", 59.918636d, 10.748033d, 2),
            new MapGlobalCity("NPL", "ne-q3037", "Kathmandu", 27.718638d, 85.314696d, 3),
            new MapGlobalCity("NZL", "ne-q23661", "Wellington", -41.299988d, 174.783266d, 3),
            new MapGlobalCity("NZL", "ne-q37100", "Auckland", -36.848055d, 174.763027d, 1),
            new MapGlobalCity("OMN", "ne-q3826", "Muscat", 23.613325d, 58.593312d, 3),
            new MapGlobalCity("PAK", "ne-q1362", "Islamabad", 33.701942d, 73.164689d, 3),
            new MapGlobalCity("PAN", "ne-q3306", "Panama City", 8.969963d, -79.534983d, 3),
            new MapGlobalCity("PER", "ne-q2868", "Lima", -12.046067d, -77.052008d, 1),
            new MapGlobalCity("PHL", "ne-q1461", "Manila", 14.606105d, 120.980271d, 1),
            new MapGlobalCity("PHL", "ne-q1822", "Baguio", 16.429991d, 120.569943d, 4),
            new MapGlobalCity("PLW", "ne-q154002", "Melekeok", 7.487396d, 134.626549d, 6),
            new MapGlobalCity("PNG", "ne-q36526", "Port Moresby", -9.464708d, 147.192504d, 3),
            new MapGlobalCity("POL", "ne-q270", "Warsaw", 52.251947d, 20.998054d, 2),
            new MapGlobalCity("PRK", "ne-q18808", "Pyongyang", 39.021385d, 125.752745d, 2),
            new MapGlobalCity("PRT", "ne-q597", "Lisbon", 38.724669d, -9.146812d, 2),
            new MapGlobalCity("PRY", "ne-q2933", "Asunción", -25.294457d, -57.643451d, 3),
            new MapGlobalCity("QAT", "ne-q3861", "Doha", 25.286556d, 51.532968d, 4),
            new MapGlobalCity("ROU", "ne-q19660", "Bucharest", 44.435318d, 26.098001d, 2),
            new MapGlobalCity("RUS", "ne-q649", "Moscow", 55.754110d, 37.613577d, 0),
            new MapGlobalCity("RWA", "ne-q3859", "Kigali", -1.951644d, 30.058586d, 4),
            new MapGlobalCity("SAH", "ne-q614754", "Bir Lehlou", 26.119167d, -9.652522d, 6),
            new MapGlobalCity("SAU", "ne-q3692", "Riyadh", 24.642779d, 46.770796d, 0),
            new MapGlobalCity("SDN", "ne-q1963", "Khartoum", 15.590024d, 32.532233d, 2),
            new MapGlobalCity("SEN", "ne-q3718", "Dakar", 14.717778d, -17.475076d, 1),
            new MapGlobalCity("SGP", "ne-q334", "Singapore", 1.294979d, 103.853875d, 0),
            new MapGlobalCity("SLB", "ne-q40921", "Honiara", -9.437994d, 159.949766d, 3),
            new MapGlobalCity("SLE", "ne-q3780", "Freetown", 8.471957d, -13.236162d, 3),
            new MapGlobalCity("SLV", "ne-q3110", "San Salvador", 13.711948d, -89.204987d, 3),
            new MapGlobalCity("SMR", "ne-q1848", "San Marino", 43.936096d, 12.441770d, 7),
            new MapGlobalCity("SOL", "ne-q168652", "Hargeisa", 9.560022d, 44.065310d, 3),
            new MapGlobalCity("SOM", "ne-q2449", "Mogadishu", 2.068627d, 45.364732d, 3),
            new MapGlobalCity("SRB", "ne-q3711", "Belgrade", 44.820591d, 20.466045d, 3),
            new MapGlobalCity("SSD", "ne-q1947", "Juba", 4.829975d, 31.580026d, 4),
            new MapGlobalCity("STP", "ne-q3932", "São Tomé", 0.333402d, 6.733325d, 3),
            new MapGlobalCity("SUR", "ne-q3001", "Paramaribo", 5.835030d, -55.167031d, 3),
            new MapGlobalCity("SVK", "ne-q1780", "Bratislava", 48.150018d, 17.116981d, 4),
            new MapGlobalCity("SVN", "ne-q437", "Ljubljana", 46.055288d, 14.514969d, 4),
            new MapGlobalCity("SWE", "ne-q1754", "Stockholm", 59.352706d, 18.095389d, 1),
            new MapGlobalCity("SWZ", "ne-q101418", "Lobamba", -26.466668d, 31.199997d, 6),
            new MapGlobalCity("SWZ", "ne-q3904", "Mbabane", -26.316651d, 31.133335d, 4),
            new MapGlobalCity("SYC", "ne-q3940", "Victoria", -4.616632d, 55.449990d, 3),
            new MapGlobalCity("SYR", "ne-q3766", "Damascus", 33.501980d, 36.298050d, 2),
            new MapGlobalCity("TCD", "ne-q3659", "N'Djamena", 12.115042d, 15.047203d, 3),
            new MapGlobalCity("TGO", "ne-q3792", "Lomé", 6.133883d, 1.220811d, 3),
            new MapGlobalCity("THA", "ne-q1861", "Bangkok", 13.751945d, 100.514699d, 1),
            new MapGlobalCity("TJK", "ne-q9365", "Dushanbe", 38.560035d, 68.773879d, 3),
            new MapGlobalCity("TKM", "ne-q23438", "Ashgabat", 37.949995d, 58.383299d, 3),
            new MapGlobalCity("TLS", "ne-q9310", "Dili", -8.559388d, 125.579456d, 3),
            new MapGlobalCity("TON", "ne-q38834", "Nuku'alofa", -21.138512d, -175.220565d, 3),
            new MapGlobalCity("TTO", "ne-q39178", "Port-of-Spain", 10.651997d, -61.517031d, 4),
            new MapGlobalCity("TUN", "ne-q3572", "Tunis", 36.802778d, 10.179678d, 3),
            new MapGlobalCity("TUR", "adana", "Adana", 36.996935d, 35.318058d, 5),
            new MapGlobalCity("TUR", "ankara", "Ankara", 39.929184d, 32.862446d, 2),
            new MapGlobalCity("TUR", "antalya", "Antalya", 36.891928d, 30.698030d, 5),
            new MapGlobalCity("TUR", "bursa", "Bursa", 40.201933d, 29.068052d, 5),
            new MapGlobalCity("TUR", "diyarbakir", "Diyarbakır", 37.920436d, 40.230040d, 5),
            new MapGlobalCity("TUR", "istanbul", "Istanbul", 41.017602d, 28.974277d, 0),
            new MapGlobalCity("TUR", "izmir", "İzmir", 38.438096d, 27.149848d, 5),
            new MapGlobalCity("TUR", "trabzon", "Trabzon", 40.979991d, 39.719994d, 5),
            new MapGlobalCity("TUV", "ne-q34126", "Funafuti", -8.516652d, 179.216647d, 6),
            new MapGlobalCity("TWN", "ne-q1867", "Taipei", 25.035833d, 121.568333d, 1),
            new MapGlobalCity("TZA", "ne-q1960", "Dar es Salaam", -6.798067d, 39.266396d, 2),
            new MapGlobalCity("TZA", "ne-q3866", "Dodoma", -6.183306d, 35.750004d, 4),
            new MapGlobalCity("UGA", "ne-q3894", "Kampala", 0.318605d, 32.581378d, 3),
            new MapGlobalCity("UKR", "ne-q1899", "Kyiv", 50.435313d, 30.514682d, 1),
            new MapGlobalCity("URY", "ne-q1335", "Montevideo", -34.856096d, -56.172998d, 3),
            new MapGlobalCity("USA", "ne-q1297", "Chicago", 41.831937d, -87.752001d, 1),
            new MapGlobalCity("USA", "ne-q16554", "Denver", 39.741134d, -104.985962d, 1),
            new MapGlobalCity("USA", "ne-q16555", "Houston", 29.821920d, -95.341925d, 1),
            new MapGlobalCity("USA", "ne-q23556", "Atlanta", 33.831960d, -84.401895d, 1),
            new MapGlobalCity("USA", "ne-q60", "New York", 40.751925d, -73.981963d, 0),
            new MapGlobalCity("USA", "ne-q61", "Washington,  D.C.", 38.901495d, -77.011364d, 0),
            new MapGlobalCity("USA", "ne-q62", "San Francisco", 37.769196d, -122.417169d, 1),
            new MapGlobalCity("USA", "ne-q65", "Los Angeles", 33.991924d, -118.181926d, 0),
            new MapGlobalCity("USA", "ne-q8652", "Miami", 25.789557d, -80.226052d, 1),
            new MapGlobalCity("UZB", "ne-q269", "Tashkent", 41.313648d, 69.292987d, 1),
            new MapGlobalCity("VAT", "ne-q237", "Vatican City", 41.903282d, 12.453387d, 8),
            new MapGlobalCity("VCT", "ne-q41474", "Kingstown", 13.148279d, -61.212062d, 4),
            new MapGlobalCity("VEN", "ne-q1533", "Caracas", 10.502944d, -66.918983d, 1),
            new MapGlobalCity("VNM", "ne-q1858", "Hanoi", 21.035273d, 105.848068d, 2),
            new MapGlobalCity("VUT", "ne-q37806", "Port Vila", -17.733350d, 168.316641d, 3),
            new MapGlobalCity("WSM", "ne-q36260", "Apia", -13.841545d, -171.738642d, 3),
            new MapGlobalCity("YEM", "ne-q2471", "Sanaa", 15.356679d, 44.204648d, 2),
            new MapGlobalCity("ZAF", "ne-q34647", "Johannesburg", -26.168099d, 28.028064d, 1),
            new MapGlobalCity("ZAF", "ne-q37701", "Bloemfontein", -29.119994d, 26.229913d, 3),
            new MapGlobalCity("ZAF", "ne-q3926", "Pretoria", -25.704975d, 28.227483d, 3),
            new MapGlobalCity("ZAF", "ne-q5465", "Cape Town", -33.918065d, 18.433042d, 0),
            new MapGlobalCity("ZMB", "ne-q3881", "Lusaka", -15.414698d, 28.281382d, 3),
            new MapGlobalCity("ZWE", "ne-q3921", "Harare", -17.815844d, 31.042764d, 3),
        };

        static readonly Dictionary<MapBusinessLocation, int> Index = BuildIndex();

        static Dictionary<MapBusinessLocation, int> BuildIndex()
        {
            var lookup = new Dictionary<MapBusinessLocation, int>(All.Length);
            for (var i = 0; i < All.Length; i++)
            {
                if (!All[i].Location.IsValid || lookup.ContainsKey(All[i].Location))
                    throw new InvalidOperationException("Invalid or duplicated global city identity");
                lookup.Add(All[i].Location, i);
            }
            return lookup;
        }

        public static bool TryGet(MapBusinessLocation location, out MapGlobalCity city)
        {
            if (location.IsValid && Index.TryGetValue(location, out var index))
            {
                city = All[index];
                return true;
            }
            city = default;
            return false;
        }
    }
}
