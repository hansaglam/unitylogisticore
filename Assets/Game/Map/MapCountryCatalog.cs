namespace LogistiCore.Map
{
    /// <summary>
    /// Natural Earth 5.1.2, ne_50m_admin_0_countries, 242 features.
    /// English is NAME. Turkish is NAME_TR. German is NAME_DE.
    /// The anchor is LABEL_X/LABEL_Y, Natural Earth's cartographic point.
        /// LABELRANK breaks collisions. MIN_LABEL and MAX_LABEL are Natural Earth's
        /// own zoom window for that name. CONTINENT is Natural Earth's continent,
        /// copied unchanged, including Türkiye as Asia.
        /// </summary>
        public static class MapCountryCatalog
        {
        public enum MapContinent : byte
        {
            None = 0,
            Africa = 1,
            Antarctica = 2,
            Asia = 3,
            Europe = 4,
            NorthAmerica = 5,
            Oceania = 6,
            SevenSeas = 7,
            SouthAmerica = 8
        }

        public readonly struct Entry
        {
            public readonly string Code;
            public readonly MapContinent Continent;
            public readonly byte Rank;
            public readonly float MinLabel;
            public readonly float MaxLabel;
            public readonly float Longitude;
            public readonly float Latitude;
            public readonly string English;
            public readonly string Turkish;
            public readonly string German;

            public Entry(
                string code,
                MapContinent continent,
                byte rank,
                float minLabel,
                float maxLabel,
                float longitude,
                float latitude,
                string english,
                string turkish,
                string german)
            {
                Code = code;
                Continent = continent;
                Rank = rank;
                MinLabel = minLabel;
                MaxLabel = maxLabel;
                Longitude = longitude;
                Latitude = latitude;
                English = english;
                Turkish = turkish;
                German = german;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry("ABW", MapContinent.NorthAmerica, 5, 5f, 10f, -69.9728f, 12.5174f, "Aruba", "Aruba", "Aruba"),
            new Entry("AFG", MapContinent.Asia, 3, 3f, 7f, 66.4966f, 34.1643f, "Afghanistan", "Afganistan", "Afghanistan"),
            new Entry("AGO", MapContinent.Africa, 3, 3f, 7f, 17.9842f, -12.1828f, "Angola", "Angola", "Angola"),
            new Entry("AIA", MapContinent.NorthAmerica, 6, 5f, 10f, -63.0264f, 18.243f, "Anguilla", "Anguilla", "Anguilla"),
            new Entry("ALB", MapContinent.Europe, 6, 5f, 10f, 20.1138f, 40.6549f, "Albania", "Arnavutluk", "Albanien"),
            new Entry("ALD", MapContinent.Europe, 6, 5f, 10f, 19.8697f, 60.1565f, "Åland", "Åland", "Åland"),
            new Entry("AND", MapContinent.Europe, 6, 5f, 10f, 1.5394f, 42.5476f, "Andorra", "Andorra", "Andorra"),
            new Entry("ARE", MapContinent.Asia, 4, 4f, 9f, 54.5473f, 23.4663f, "United Arab Emirates", "Birleşik Arap Emirlikleri", "Vereinigte Arabische Emirate"),
            new Entry("ARG", MapContinent.SouthAmerica, 2, 2f, 7f, -64.1733f, -33.5012f, "Argentina", "Arjantin", "Argentinien"),
            new Entry("ARM", MapContinent.Asia, 6, 5f, 10f, 44.8006f, 40.4591f, "Armenia", "Ermenistan", "Armenien"),
            new Entry("ASM", MapContinent.Oceania, 4, 4f, 9f, -170.7472f, -14.3267f, "American Samoa", "Amerikan Samoası", "Amerikanisch-Samoa"),
            new Entry("ATA", MapContinent.Antarctica, 4, 4f, 9f, 35.8855f, -79.8432f, "Antarctica", "Antarktika", "Antarktika"),
            new Entry("ATC", MapContinent.Oceania, 5, 4.5f, 9.5f, 123.5864f, -12.4326f, "Ashmore and Cartier Is.", "Ashmore ve Cartier Adaları", "Ashmore- und Cartierinseln"),
            new Entry("ATF", MapContinent.SevenSeas, 6, 4f, 9f, 69.1221f, -49.3037f, "Fr. S. Antarctic Lands", "Fransız Güney ve Antarktika Toprakları", "Französische Süd- und Antarktisgebiete"),
            new Entry("ATG", MapContinent.NorthAmerica, 6, 5f, 9.5f, -61.7906f, 17.3522f, "Antigua and Barb.", "Antigua ve Barbuda", "Antigua und Barbuda"),
            new Entry("AUS", MapContinent.Oceania, 2, 1.7f, 5.7f, 134.0497f, -24.1295f, "Australia", "Avustralya", "Australien"),
            new Entry("AUT", MapContinent.Europe, 4, 3f, 8f, 14.1305f, 47.5189f, "Austria", "Avusturya", "Österreich"),
            new Entry("AZE", MapContinent.Asia, 5, 4f, 9f, 47.211f, 40.4024f, "Azerbaijan", "Azerbaycan", "Aserbaidschan"),
            new Entry("BDI", MapContinent.Africa, 6, 4f, 9f, 29.9171f, -3.3328f, "Burundi", "Burundi", "Burundi"),
            new Entry("BEL", MapContinent.Europe, 2, 4f, 9f, 4.8004f, 50.7854f, "Belgium", "Belçika", "Belgien"),
            new Entry("BEN", MapContinent.Africa, 5, 4f, 9f, 2.352f, 10.3248f, "Benin", "Benin", "Benin"),
            new Entry("BFA", MapContinent.Africa, 3, 3f, 8f, -1.3639f, 12.673f, "Burkina Faso", "Burkina Faso", "Burkina Faso"),
            new Entry("BGD", MapContinent.Asia, 3, 3f, 8f, 89.685f, 24.215f, "Bangladesh", "Bangladeş", "Bangladesch"),
            new Entry("BGR", MapContinent.Europe, 4, 4f, 9f, 25.1571f, 42.5088f, "Bulgaria", "Bulgaristan", "Bulgarien"),
            new Entry("BHR", MapContinent.Asia, 4, 4f, 9f, 50.5548f, 26.056f, "Bahrain", "Bahreyn", "Bahrain"),
            new Entry("BHS", MapContinent.NorthAmerica, 4, 4f, 9f, -77.1467f, 26.4018f, "Bahamas", "Bahamalar", "Bahamas"),
            new Entry("BIH", MapContinent.Europe, 5, 4.5f, 6.8f, 18.0684f, 44.0911f, "Bosnia and Herz.", "Bosna-Hersek", "Bosnien und Herzegowina"),
            new Entry("BLM", MapContinent.NorthAmerica, 6, 5.7f, 10f, -62.8332f, 17.902f, "St-Barthélemy", "Saint Barthélemy", "Saint-Barthélemy"),
            new Entry("BLR", MapContinent.Europe, 4, 3f, 8f, 28.4177f, 53.8219f, "Belarus", "Beyaz Rusya", "Belarus"),
            new Entry("BLZ", MapContinent.NorthAmerica, 6, 5f, 10f, -88.713f, 17.2021f, "Belize", "Belize", "Belize"),
            new Entry("BMU", MapContinent.NorthAmerica, 6, 4f, 9f, -64.7636f, 32.2966f, "Bermuda", "Bermuda", "Bermuda"),
            new Entry("BOL", MapContinent.SouthAmerica, 3, 3f, 7.5f, -64.5934f, -16.666f, "Bolivia", "Bolivya", "Bolivien"),
            new Entry("BRA", MapContinent.SouthAmerica, 2, 1.7f, 5.7f, -49.5594f, -12.0987f, "Brazil", "Brezilya", "Brasilien"),
            new Entry("BRB", MapContinent.NorthAmerica, 5, 4.5f, 9.5f, -59.569f, 13.1637f, "Barbados", "Barbados", "Barbados"),
            new Entry("BRN", MapContinent.Asia, 6, 4f, 9f, 114.5519f, 4.4483f, "Brunei", "Brunei", "Brunei"),
            new Entry("BTN", MapContinent.Asia, 5, 4f, 9f, 90.0403f, 27.5367f, "Bhutan", "Bhutan", "Bhutan"),
            new Entry("BWA", MapContinent.Africa, 4, 4f, 9f, 24.1792f, -22.1026f, "Botswana", "Botsvana", "Botswana"),
            new Entry("CAF", MapContinent.Africa, 4, 4f, 9f, 20.9069f, 6.9897f, "Central African Rep.", "Orta Afrika Cumhuriyeti", "Zentralafrikanische Republik"),
            new Entry("CAN", MapContinent.NorthAmerica, 2, 1.7f, 5.7f, -101.9107f, 60.3243f, "Canada", "Kanada", "Kanada"),
            new Entry("CHE", MapContinent.Europe, 4, 4f, 9f, 7.464f, 46.7191f, "Switzerland", "İsviçre", "Schweiz"),
            new Entry("CHL", MapContinent.SouthAmerica, 2, 1.7f, 6.7f, -72.3189f, -38.1518f, "Chile", "Şili", "Chile"),
            new Entry("CHN", MapContinent.Asia, 2, 1.7f, 5.7f, 106.3373f, 32.4982f, "China", "Çin Halk Cumhuriyeti", "Volksrepublik China"),
            new Entry("CIV", MapContinent.Africa, 3, 2.5f, 8f, -5.5686f, 7.4914f, "Côte d'Ivoire", "Fildişi Sahili", "Elfenbeinküste"),
            new Entry("CMR", MapContinent.Africa, 3, 3f, 8f, 12.4735f, 4.585f, "Cameroon", "Kamerun", "Kamerun"),
            new Entry("COD", MapContinent.Africa, 2, 2f, 7f, 23.4588f, -1.8582f, "Dem. Rep. Congo", "Demokratik Kongo Cumhuriyeti", "Demokratische Republik Kongo"),
            new Entry("COG", MapContinent.Africa, 4, 4f, 9f, 15.9005f, 0.1423f, "Congo", "Kongo Cumhuriyeti", "Republik Kongo"),
            new Entry("COK", MapContinent.Oceania, 4, 4f, 9f, -159.7857f, -21.216f, "Cook Is.", "Cook Adaları", "Cookinseln"),
            new Entry("COL", MapContinent.SouthAmerica, 2, 3f, 7f, -73.1743f, 3.3731f, "Colombia", "Kolombiya", "Kolumbien"),
            new Entry("COM", MapContinent.Africa, 6, 4f, 9f, 43.3181f, -11.7277f, "Comoros", "Komorlar", "Komoren"),
            new Entry("CPV", MapContinent.Africa, 4, 4f, 9f, -23.6394f, 15.0748f, "Cabo Verde", "Yeşil Burun Adaları", "Kap Verde"),
            new Entry("CRI", MapContinent.NorthAmerica, 5, 2.5f, 8f, -84.0779f, 10.0651f, "Costa Rica", "Kosta Rika", "Costa Rica"),
            new Entry("CUB", MapContinent.NorthAmerica, 3, 2.7f, 8f, -77.9759f, 21.334f, "Cuba", "Küba", "Kuba"),
            new Entry("CUW", MapContinent.NorthAmerica, 5, 5f, 10f, -68.9206f, 12.145f, "Curaçao", "Curaçao", "Curaçao"),
            new Entry("CYM", MapContinent.NorthAmerica, 5, 5f, 9.5f, -81.2405f, 19.3199f, "Cayman Is.", "Cayman Adaları", "Cayman Islands"),
            new Entry("CYN", MapContinent.Asia, 6, 6f, 10f, 33.6924f, 35.2161f, "N. Cyprus", "Kuzey Kıbrıs Türk Cumhuriyeti", "Türkische Republik Nordzypern"),
            new Entry("CYP", MapContinent.Asia, 5, 4.5f, 9.5f, 33.0842f, 34.9133f, "Cyprus", "Kıbrıs Cumhuriyeti", "Republik Zypern"),
            new Entry("CZE", MapContinent.Europe, 5, 4f, 9f, 15.3776f, 49.8824f, "Czechia", "Çek Cumhuriyeti", "Tschechien"),
            new Entry("DEU", MapContinent.Europe, 2, 1.7f, 6.7f, 9.6783f, 50.9617f, "Germany", "Almanya", "Deutschland"),
            new Entry("DJI", MapContinent.Africa, 5, 4f, 9f, 42.4988f, 11.9763f, "Djibouti", "Cibuti", "Dschibuti"),
            new Entry("DMA", MapContinent.NorthAmerica, 6, 4f, 9f, -61.345f, 15.4588f, "Dominica", "Dominika", "Dominica"),
            new Entry("DNK", MapContinent.Europe, 4, 3f, 8f, 9.0182f, 55.967f, "Denmark", "Danimarka", "Dänemark"),
            new Entry("DOM", MapContinent.NorthAmerica, 5, 4.5f, 9.5f, -70.654f, 19.1041f, "Dominican Rep.", "Dominik Cumhuriyeti", "Dominikanische Republik"),
            new Entry("DZA", MapContinent.Africa, 3, 2.5f, 7f, 2.8082f, 27.3974f, "Algeria", "Cezayir", "Algerien"),
            new Entry("ECU", MapContinent.SouthAmerica, 3, 3f, 8f, -78.1884f, -1.2591f, "Ecuador", "Ekvador", "Ecuador"),
            new Entry("EGY", MapContinent.Africa, 2, 1.7f, 6.7f, 29.4458f, 26.1862f, "Egypt", "Mısır", "Ägypten"),
            new Entry("ERI", MapContinent.Africa, 4, 4f, 9f, 38.2856f, 15.7874f, "Eritrea", "Eritre", "Eritrea"),
            new Entry("ESP", MapContinent.Europe, 2, 2f, 7f, -3.4647f, 40.091f, "Spain", "İspanya", "Spanien"),
            new Entry("EST", MapContinent.Europe, 6, 3f, 8f, 25.8671f, 58.7249f, "Estonia", "Estonya", "Estland"),
            new Entry("ETH", MapContinent.Africa, 2, 2f, 7f, 39.0886f, 8.0328f, "Ethiopia", "Etiyopya", "Äthiopien"),
            new Entry("FIN", MapContinent.Europe, 3, 3f, 8f, 27.2764f, 63.2524f, "Finland", "Finlandiya", "Finnland"),
            new Entry("FJI", MapContinent.Oceania, 6, 3f, 8f, 177.9754f, -17.8261f, "Fiji", "Fiji", "Fidschi"),
            new Entry("FLK", MapContinent.SouthAmerica, 5, 4.5f, 9f, -58.7386f, -51.6089f, "Falkland Is.", "Falkland Adaları", "Falklandinseln"),
            new Entry("FRA", MapContinent.Europe, 2, 1.7f, 6.7f, 2.5523f, 46.6961f, "France", "Fransa", "Frankreich"),
            new Entry("FRO", MapContinent.Europe, 6, 4f, 9f, -7.0584f, 62.1856f, "Faeroe Is.", "Faroe Adaları", "Färöer"),
            new Entry("FSM", MapContinent.Oceania, 6, 5f, 10f, 158.234f, 6.8876f, "Micronesia", "Mikronezya", "Föderierte Staaten von Mikronesien"),
            new Entry("GAB", MapContinent.Africa, 4, 3f, 8f, 11.8359f, -0.4377f, "Gabon", "Gabon", "Gabun"),
            new Entry("GBR", MapContinent.Europe, 2, 1.7f, 6.7f, -2.1163f, 54.4027f, "United Kingdom", "Birleşik Krallık", "Vereinigtes Königreich"),
            new Entry("GEO", MapContinent.Asia, 5, 4f, 9f, 43.7357f, 41.8701f, "Georgia", "Gürcistan", "Georgien"),
            new Entry("GGY", MapContinent.Europe, 6, 5f, 10f, -2.5617f, 49.4635f, "Guernsey", "Guernsey", "Guernsey"),
            new Entry("GHA", MapContinent.Africa, 3, 2.7f, 8f, -1.0369f, 7.7176f, "Ghana", "Gana", "Ghana"),
            new Entry("GIN", MapContinent.Africa, 3, 3f, 8f, -10.0164f, 10.6185f, "Guinea", "Gine", "Guinea"),
            new Entry("GMB", MapContinent.Africa, 6, 5f, 10f, -14.9983f, 13.6417f, "Gambia", "Gambiya", "Gambia"),
            new Entry("GNB", MapContinent.Africa, 6, 5f, 10f, -14.5241f, 12.1637f, "Guinea-Bissau", "Gine-Bissau", "Guinea-Bissau"),
            new Entry("GNQ", MapContinent.Africa, 4, 4f, 9f, 8.9902f, 2.333f, "Eq. Guinea", "Ekvator Ginesi", "Äquatorialguinea"),
            new Entry("GRC", MapContinent.Europe, 3, 2.7f, 8f, 21.7257f, 39.4928f, "Greece", "Yunanistan", "Griechenland"),
            new Entry("GRD", MapContinent.NorthAmerica, 6, 4f, 9f, -61.6805f, 12.1132f, "Grenada", "Grenada", "Grenada"),
            new Entry("GRL", MapContinent.NorthAmerica, 3, 1.7f, 6.7f, -39.3353f, 74.3194f, "Greenland", "Grönland", "Grönland"),
            new Entry("GTM", MapContinent.NorthAmerica, 3, 3f, 8f, -90.4971f, 14.9821f, "Guatemala", "Guatemala", "Guatemala"),
            new Entry("GUM", MapContinent.Oceania, 6, 3f, 10f, 144.7036f, 13.3542f, "Guam", "Guam", "Guam"),
            new Entry("GUY", MapContinent.SouthAmerica, 4, 4f, 9f, -58.9426f, 5.1243f, "Guyana", "Guyana", "Guyana"),
            new Entry("HKG", MapContinent.Asia, 4, 4f, 9f, 114.0978f, 22.4488f, "Hong Kong", "Hong Kong", "Hongkong"),
            new Entry("HMD", MapContinent.SevenSeas, 5, 4.5f, 9.5f, 73.5052f, -53.1035f, "Heard I. and McDonald Is.", "Heard Adası ve McDonald Adaları", "Heard und McDonaldinseln"),
            new Entry("HND", MapContinent.NorthAmerica, 5, 4.5f, 9.5f, -86.8876f, 14.7948f, "Honduras", "Honduras", "Honduras"),
            new Entry("HRV", MapContinent.Europe, 6, 4f, 9f, 16.3724f, 45.8058f, "Croatia", "Hırvatistan", "Kroatien"),
            new Entry("HTI", MapContinent.NorthAmerica, 5, 4f, 9f, -72.2241f, 19.2638f, "Haiti", "Haiti", "Haiti"),
            new Entry("HUN", MapContinent.Europe, 5, 4f, 9f, 19.4479f, 47.0868f, "Hungary", "Macaristan", "Ungarn"),
            new Entry("IDN", MapContinent.Asia, 2, 1.7f, 6.7f, 101.8929f, -0.9544f, "Indonesia", "Endonezya", "Indonesien"),
            new Entry("IMN", MapContinent.Europe, 6, 5f, 10f, -4.5301f, 54.2208f, "Isle of Man", "Man Adası", "Isle of Man"),
            new Entry("IND", MapContinent.Asia, 2, 1.7f, 6.7f, 79.3581f, 22.6869f, "India", "Hindistan", "Indien"),
            new Entry("IOA", MapContinent.Asia, 5, 5f, 9.5f, 105.6726f, -10.4908f, "Indian Ocean Ter.", "Australian Indian Ocean Territories", "Australische Territorien im Indischen Ozean"),
            new Entry("IOT", MapContinent.SevenSeas, 5, 5f, 9.5f, 71.3483f, -6.1908f, "Br. Indian Ocean Ter.", "Britanya Hint Okyanusu Toprakları", "Britisches Territorium im Indischen Ozean"),
            new Entry("IRL", MapContinent.Europe, 3, 3f, 8f, -7.7986f, 53.0787f, "Ireland", "İrlanda", "Irland"),
            new Entry("IRN", MapContinent.Asia, 2, 2.5f, 6.7f, 54.9315f, 32.1662f, "Iran", "İran", "Iran"),
            new Entry("IRQ", MapContinent.Asia, 3, 3f, 7.5f, 43.2618f, 33.094f, "Iraq", "Irak", "Irak"),
            new Entry("ISL", MapContinent.Europe, 3, 2f, 7f, -18.6737f, 64.7793f, "Iceland", "İzlanda", "Island"),
            new Entry("ISR", MapContinent.Asia, 4, 3f, 8f, 34.8479f, 30.9111f, "Israel", "İsrail", "Israel"),
            new Entry("ITA", MapContinent.Europe, 2, 2f, 7f, 11.0769f, 44.7325f, "Italy", "İtalya", "Italien"),
            new Entry("JAM", MapContinent.NorthAmerica, 4, 4f, 9f, -77.3188f, 18.1371f, "Jamaica", "Jamaika", "Jamaika"),
            new Entry("JEY", MapContinent.Europe, 6, 5f, 10f, -2.0901f, 49.2208f, "Jersey", "Jersey", "Jersey"),
            new Entry("JOR", MapContinent.Asia, 4, 4f, 9f, 36.376f, 30.805f, "Jordan", "Ürdün", "Jordanien"),
            new Entry("JPN", MapContinent.Asia, 2, 1.7f, 7f, 138.4422f, 36.1425f, "Japan", "Japonya", "Japan"),
            new Entry("KAS", MapContinent.Asia, 5, 6.5f, 9.5f, 77.1296f, 35.3406f, "Siachen Glacier", "Siachen Buzulu", "Siachen-Gletscher"),
            new Entry("KAZ", MapContinent.Asia, 3, 2.7f, 7f, 68.6855f, 49.0541f, "Kazakhstan", "Kazakistan", "Kasachstan"),
            new Entry("KEN", MapContinent.Africa, 2, 1.7f, 6.7f, 37.9076f, 0.549f, "Kenya", "Kenya", "Kenia"),
            new Entry("KGZ", MapContinent.Asia, 4, 3f, 8f, 74.5326f, 41.6685f, "Kyrgyzstan", "Kırgızistan", "Kirgisistan"),
            new Entry("KHM", MapContinent.Asia, 3, 3f, 8f, 104.5049f, 12.6476f, "Cambodia", "Kamboçya", "Kambodscha"),
            new Entry("KIR", MapContinent.Oceania, 6, 5f, 10f, -157.3846f, 1.8204f, "Kiribati", "Kiribati", "Kiribati"),
            new Entry("KNA", MapContinent.NorthAmerica, 6, 5f, 10f, -62.758f, 17.3366f, "St. Kitts and Nevis", "Saint Kitts ve Nevis", "St. Kitts und Nevis"),
            new Entry("KOR", MapContinent.Asia, 2, 2.5f, 7f, 128.1295f, 36.3849f, "South Korea", "Güney Kore", "Südkorea"),
            new Entry("KOS", MapContinent.Europe, 6, 5f, 10f, 20.8607f, 42.5936f, "Kosovo", "Kosova", "Kosovo"),
            new Entry("KWT", MapContinent.Asia, 6, 5f, 10f, 47.314f, 29.4136f, "Kuwait", "Kuveyt", "Kuwait"),
            new Entry("LAO", MapContinent.Asia, 4, 4f, 9f, 102.5339f, 19.4318f, "Laos", "Laos", "Laos"),
            new Entry("LBN", MapContinent.Asia, 5, 4f, 9f, 35.9929f, 34.1334f, "Lebanon", "Lübnan", "Libanon"),
            new Entry("LBR", MapContinent.Africa, 4, 4f, 9f, -9.4604f, 6.4472f, "Liberia", "Liberya", "Liberia"),
            new Entry("LBY", MapContinent.Africa, 3, 3f, 8f, 18.011f, 26.6389f, "Libya", "Libya", "Libyen"),
            new Entry("LCA", MapContinent.NorthAmerica, 6, 5f, 9.5f, -60.9801f, 13.8924f, "Saint Lucia", "Saint Lucia", "St. Lucia"),
            new Entry("LIE", MapContinent.Europe, 6, 5f, 10f, 9.5594f, 47.1114f, "Liechtenstein", "Lihtenştayn", "Liechtenstein"),
            new Entry("LKA", MapContinent.Asia, 3, 3f, 8f, 80.7048f, 7.5811f, "Sri Lanka", "Sri Lanka", "Sri Lanka"),
            new Entry("LSO", MapContinent.Africa, 6, 4f, 9f, 28.2466f, -29.4802f, "Lesotho", "Lesotho", "Lesotho"),
            new Entry("LTU", MapContinent.Europe, 5, 4f, 9f, 24.0899f, 55.1037f, "Lithuania", "Litvanya", "Litauen"),
            new Entry("LUX", MapContinent.Europe, 6, 5.7f, 10f, 6.0776f, 49.7337f, "Luxembourg", "Lüksemburg", "Luxemburg"),
            new Entry("LVA", MapContinent.Europe, 5, 4f, 9f, 25.4587f, 57.0669f, "Latvia", "Letonya", "Lettland"),
            new Entry("MAC", MapContinent.Asia, 4, 4f, 9f, 113.556f, 22.1297f, "Macao", "Makao", "Macau"),
            new Entry("MAF", MapContinent.NorthAmerica, 6, 5f, 10f, -63.0494f, 18.0813f, "St-Martin", "Saint Martin", "Saint-Martin"),
            new Entry("MAR", MapContinent.Africa, 3, 2.7f, 8f, -7.1873f, 31.6507f, "Morocco", "Fas", "Marokko"),
            new Entry("MCO", MapContinent.Europe, 6, 5f, 10f, 7.3983f, 43.7397f, "Monaco", "Monako", "Monaco"),
            new Entry("MDA", MapContinent.Europe, 6, 5f, 10f, 28.4879f, 47.435f, "Moldova", "Moldova", "Republik Moldau"),
            new Entry("MDG", MapContinent.Africa, 3, 2.7f, 7f, 46.7042f, -18.6283f, "Madagascar", "Madagaskar", "Madagaskar"),
            new Entry("MDV", MapContinent.SevenSeas, 5, 4f, 9f, 73.5076f, 4.1744f, "Maldives", "Maldivler", "Malediven"),
            new Entry("MEX", MapContinent.NorthAmerica, 2, 2f, 6.7f, -102.2894f, 23.92f, "Mexico", "Meksika", "Mexiko"),
            new Entry("MHL", MapContinent.Oceania, 6, 5f, 10f, 171.1936f, 7.0826f, "Marshall Is.", "Marshall Adaları", "Marshallinseln"),
            new Entry("MKD", MapContinent.Europe, 6, 5f, 10f, 21.5558f, 41.5582f, "North Macedonia", "Kuzey Makedonya", "Nordmazedonien"),
            new Entry("MLI", MapContinent.Africa, 3, 3f, 7f, -2.0385f, 18.6927f, "Mali", "Mali", "Mali"),
            new Entry("MLT", MapContinent.Europe, 5, 4f, 9f, 14.433f, 35.8929f, "Malta", "Malta", "Malta"),
            new Entry("MMR", MapContinent.Asia, 3, 3f, 8f, 95.8045f, 21.5739f, "Myanmar", "Myanmar", "Myanmar"),
            new Entry("MNE", MapContinent.Europe, 6, 5f, 10f, 19.1437f, 42.8031f, "Montenegro", "Karadağ", "Montenegro"),
            new Entry("MNG", MapContinent.Asia, 3, 3f, 7f, 104.1504f, 45.9975f, "Mongolia", "Moğolistan", "Mongolei"),
            new Entry("MNP", MapContinent.Oceania, 6, 5f, 10f, 145.7344f, 15.1882f, "N. Mariana Is.", "Kuzey Mariana Adaları", "Nördliche Marianen"),
            new Entry("MOZ", MapContinent.Africa, 3, 3f, 8f, 37.8379f, -13.9432f, "Mozambique", "Mozambik", "Mosambik"),
            new Entry("MRT", MapContinent.Africa, 3, 3f, 8f, -9.7403f, 19.5871f, "Mauritania", "Moritanya", "Mauretanien"),
            new Entry("MSR", MapContinent.NorthAmerica, 6, 5f, 10f, -62.1883f, 16.7372f, "Montserrat", "Montserrat", "Montserrat"),
            new Entry("MUS", MapContinent.SevenSeas, 5, 4f, 9f, 57.5658f, -20.2995f, "Mauritius", "Mauritius", "Mauritius"),
            new Entry("MWI", MapContinent.Africa, 6, 4f, 9f, 33.6081f, -13.3867f, "Malawi", "Malavi", "Malawi"),
            new Entry("MYS", MapContinent.Asia, 3, 3f, 8f, 113.8371f, 2.5287f, "Malaysia", "Malezya", "Malaysia"),
            new Entry("NAM", MapContinent.Africa, 3, 3f, 7.5f, 17.1082f, -20.5753f, "Namibia", "Namibya", "Namibia"),
            new Entry("NCL", MapContinent.Oceania, 3, 4.6f, 8f, 165.084f, -21.0647f, "New Caledonia", "Yeni Kaledonya", "Neukaledonien"),
            new Entry("NER", MapContinent.Africa, 3, 3f, 8f, 9.5044f, 17.4462f, "Niger", "Nijer", "Niger"),
            new Entry("NFK", MapContinent.Oceania, 5, 4.5f, 9.5f, 167.9545f, -29.033f, "Norfolk Island", "Norfolk Adası", "Norfolkinsel"),
            new Entry("NGA", MapContinent.Africa, 2, 1.7f, 6.7f, 7.5032f, 9.4398f, "Nigeria", "Nijerya", "Nigeria"),
            new Entry("NIC", MapContinent.NorthAmerica, 5, 4f, 9f, -85.0693f, 12.6707f, "Nicaragua", "Nikaragua", "Nicaragua"),
            new Entry("NIU", MapContinent.Oceania, 4, 4f, 9f, -169.8626f, -19.046f, "Niue", "Niue", "Niue"),
            new Entry("NLD", MapContinent.Europe, 5, 4f, 10f, 5.6114f, 52.4222f, "Netherlands", "Hollanda", "Niederlande"),
            new Entry("NOR", MapContinent.Europe, 3, 3f, 7f, 9.68f, 61.3571f, "Norway", "Norveç", "Norwegen"),
            new Entry("NPL", MapContinent.Asia, 3, 3f, 8f, 83.6399f, 28.2979f, "Nepal", "Nepal", "Nepal"),
            new Entry("NRU", MapContinent.Oceania, 6, 5f, 10f, 166.9326f, -0.5203f, "Nauru", "Nauru", "Nauru"),
            new Entry("NZL", MapContinent.Oceania, 2, 2f, 6.7f, 172.787f, -39.759f, "New Zealand", "Yeni Zelanda", "Neuseeland"),
            new Entry("OMN", MapContinent.Asia, 4, 4f, 9f, 57.3366f, 22.1204f, "Oman", "Umman", "Oman"),
            new Entry("PAK", MapContinent.Asia, 2, 2.7f, 7f, 68.5456f, 29.3284f, "Pakistan", "Pakistan", "Pakistan"),
            new Entry("PAN", MapContinent.NorthAmerica, 4, 4f, 9f, -80.3521f, 8.722f, "Panama", "Panama", "Panama"),
            new Entry("PCN", MapContinent.Oceania, 4, 5f, 9f, -128.3175f, -24.3646f, "Pitcairn Is.", "Pitcairn Adaları", "Pitcairninseln"),
            new Entry("PER", MapContinent.SouthAmerica, 2, 2f, 7f, -72.9002f, -12.9767f, "Peru", "Peru", "Peru"),
            new Entry("PHL", MapContinent.Asia, 2, 2.5f, 7f, 122.465f, 11.198f, "Philippines", "Filipinler", "Philippinen"),
            new Entry("PLW", MapContinent.Oceania, 6, 5f, 10f, 134.5802f, 7.5183f, "Palau", "Palau", "Palau"),
            new Entry("PNG", MapContinent.Oceania, 2, 2.5f, 7.5f, 143.9102f, -5.6953f, "Papua New Guinea", "Papua Yeni Gine", "Papua-Neuguinea"),
            new Entry("POL", MapContinent.Europe, 3, 2.5f, 7f, 19.4905f, 51.9903f, "Poland", "Polonya", "Polen"),
            new Entry("PRI", MapContinent.NorthAmerica, 5, 3f, 8f, -66.4811f, 18.2347f, "Puerto Rico", "Porto Riko", "Puerto Rico"),
            new Entry("PRK", MapContinent.Asia, 3, 3f, 8f, 126.4445f, 39.8853f, "North Korea", "Kuzey Kore", "Nordkorea"),
            new Entry("PRT", MapContinent.Europe, 2, 3f, 8f, -8.2718f, 39.6067f, "Portugal", "Portekiz", "Portugal"),
            new Entry("PRY", MapContinent.SouthAmerica, 4, 3f, 8f, -60.1464f, -21.6745f, "Paraguay", "Paraguay", "Paraguay"),
            new Entry("PSX", MapContinent.Asia, 5, 4.5f, 9.5f, 35.2913f, 32.0474f, "Palestine", "Filistin", "Palästina"),
            new Entry("PYF", MapContinent.Oceania, 4, 3.5f, 8.5f, -149.4616f, -17.6281f, "Fr. Polynesia", "Fransız Polinezyası", "Französisch-Polynesien"),
            new Entry("QAT", MapContinent.Asia, 5, 4f, 9f, 51.1435f, 25.2374f, "Qatar", "Katar", "Katar"),
            new Entry("ROU", MapContinent.Europe, 3, 3f, 8f, 24.9726f, 45.7332f, "Romania", "Romanya", "Rumänien"),
            new Entry("RUS", MapContinent.Europe, 2, 1.7f, 5.2f, 44.6865f, 58.2494f, "Russia", "Rusya", "Russland"),
            new Entry("RWA", MapContinent.Africa, 3, 3f, 8f, 30.1039f, -1.8972f, "Rwanda", "Ruanda", "Ruanda"),
            new Entry("SAH", MapContinent.Africa, 7, 6f, 11f, -12.6303f, 23.9676f, "W. Sahara", "Batı Sahra", "Westsahara"),
            new Entry("SAU", MapContinent.Asia, 2, 1.7f, 7f, 44.6996f, 23.8069f, "Saudi Arabia", "Suudi Arabistan", "Saudi-Arabien"),
            new Entry("SDN", MapContinent.Africa, 3, 2.5f, 8f, 29.2607f, 16.3307f, "Sudan", "Sudan", "Sudan"),
            new Entry("SDS", MapContinent.Africa, 3, 3f, 8f, 30.3902f, 7.2305f, "S. Sudan", "Güney Sudan", "Südsudan"),
            new Entry("SEN", MapContinent.Africa, 3, 2.7f, 8f, -14.7786f, 15.1381f, "Senegal", "Senegal", "Senegal"),
            new Entry("SGP", MapContinent.Asia, 6, 4f, 9f, 103.8169f, 1.3666f, "Singapore", "Singapur", "Singapur"),
            new Entry("SGS", MapContinent.SevenSeas, 4, 5f, 9f, -31.0632f, -55.6834f, "S. Geo. and the Is.", "Güney Georgia ve Güney Sandwich Adaları", "Südgeorgien und die Südlichen Sandwichinseln"),
            new Entry("SHN", MapContinent.SevenSeas, 6, 5f, 10f, -5.7126f, -15.9505f, "Saint Helena", "Saint Helena", "St. Helena"),
            new Entry("SLB", MapContinent.Oceania, 3, 3f, 8f, 159.1705f, -8.0295f, "Solomon Is.", "Solomon Adaları", "Salomonen"),
            new Entry("SLE", MapContinent.Africa, 4, 4f, 9f, -11.7637f, 8.6174f, "Sierra Leone", "Sierra Leone", "Sierra Leone"),
            new Entry("SLV", MapContinent.NorthAmerica, 6, 5f, 10f, -88.8901f, 13.6854f, "El Salvador", "El Salvador", "El Salvador"),
            new Entry("SMR", MapContinent.Europe, 6, 5f, 10f, 12.4412f, 43.9339f, "San Marino", "San Marino", "San Marino"),
            new Entry("SOL", MapContinent.Africa, 5, 4.5f, 9f, 46.7316f, 9.4439f, "Somaliland", "Somaliland", "Somaliland"),
            new Entry("SOM", MapContinent.Africa, 6, 4f, 9f, 45.1924f, 3.5689f, "Somalia", "Somali", "Somalia"),
            new Entry("SPM", MapContinent.NorthAmerica, 4, 5f, 10f, -56.3324f, 47.0403f, "St. Pierre and Miquelon", "Saint Pierre ve Miquelon", "Saint-Pierre und Miquelon"),
            new Entry("SRB", MapContinent.Europe, 5, 4f, 7f, 20.788f, 44.1899f, "Serbia", "Sırbistan", "Serbien"),
            new Entry("STP", MapContinent.Africa, 6, 5f, 10f, 7.021f, 0.9709f, "São Tomé and Principe", "São Tomé ve Príncipe", "São Tomé und Príncipe"),
            new Entry("SUR", MapContinent.SouthAmerica, 4, 4f, 9f, -55.9109f, 4.144f, "Suriname", "Surinam", "Suriname"),
            new Entry("SVK", MapContinent.Europe, 6, 4f, 9f, 19.0499f, 48.734f, "Slovakia", "Slovakya", "Slowakei"),
            new Entry("SVN", MapContinent.Europe, 6, 5f, 10f, 14.9153f, 46.0608f, "Slovenia", "Slovenya", "Slowenien"),
            new Entry("SWE", MapContinent.Europe, 3, 2f, 7f, 19.0171f, 65.8592f, "Sweden", "İsveç", "Schweden"),
            new Entry("SWZ", MapContinent.Africa, 4, 4f, 9f, 31.4673f, -26.5337f, "eSwatini", "Esvatini", "Eswatini"),
            new Entry("SXM", MapContinent.NorthAmerica, 6, 5f, 10f, -63.0701f, 18.0409f, "Sint Maarten", "Sint Maarten", "Sint Maarten"),
            new Entry("SYC", MapContinent.SevenSeas, 6, 5f, 10f, 55.4802f, -4.6767f, "Seychelles", "Seyşeller", "Seychellen"),
            new Entry("SYR", MapContinent.Asia, 3, 3f, 8f, 38.2778f, 35.0066f, "Syria", "Suriye", "Syrien"),
            new Entry("TCA", MapContinent.NorthAmerica, 6, 5f, 10f, -71.7527f, 21.8166f, "Turks and Caicos Is.", "Turks ve Caicos Adaları", "Turks- und Caicosinseln"),
            new Entry("TCD", MapContinent.Africa, 3, 3f, 8f, 18.645f, 15.143f, "Chad", "Çad", "Tschad"),
            new Entry("TGO", MapContinent.Africa, 6, 5f, 10f, 1.0581f, 8.8072f, "Togo", "Togo", "Togo"),
            new Entry("THA", MapContinent.Asia, 3, 2.7f, 8f, 101.0732f, 15.4597f, "Thailand", "Tayland", "Thailand"),
            new Entry("TJK", MapContinent.Asia, 4, 4f, 9f, 72.5873f, 38.1998f, "Tajikistan", "Tacikistan", "Tadschikistan"),
            new Entry("TKM", MapContinent.Asia, 4, 3f, 8f, 58.6766f, 39.8552f, "Turkmenistan", "Türkmenistan", "Turkmenistan"),
            new Entry("TLS", MapContinent.Asia, 5, 4f, 9f, 125.8547f, -8.8037f, "Timor-Leste", "Doğu Timor", "Osttimor"),
            new Entry("TON", MapContinent.Oceania, 4, 4f, 9f, -175.163f, -21.21f, "Tonga", "Tonga", "Tonga"),
            new Entry("TTO", MapContinent.NorthAmerica, 5, 4.5f, 9.5f, -60.9184f, 10.9989f, "Trinidad and Tobago", "Trinidad ve Tobago", "Trinidad und Tobago"),
            new Entry("TUN", MapContinent.Africa, 3, 3f, 8f, 9.0079f, 33.6873f, "Tunisia", "Tunus", "Tunesien"),
            new Entry("TUR", MapContinent.Asia, 2, 2f, 7f, 34.5083f, 39.3454f, "Turkey", "Türkiye", "Türkei"),
            new Entry("TUV", MapContinent.Oceania, 6, 5f, 10f, 179.2096f, -8.5137f, "Tuvalu", "Tuvalu", "Tuvalu"),
            new Entry("TWN", MapContinent.Asia, 3, 4.5f, 8f, 120.8682f, 23.6524f, "Taiwan", "Çin Cumhuriyeti", "Republik China"),
            new Entry("TZA", MapContinent.Africa, 3, 3f, 8f, 34.9592f, -6.0519f, "Tanzania", "Tanzanya", "Tansania"),
            new Entry("UGA", MapContinent.Africa, 3, 3f, 8f, 32.9486f, 1.9726f, "Uganda", "Uganda", "Uganda"),
            new Entry("UKR", MapContinent.Europe, 3, 2.7f, 7f, 32.1409f, 49.7247f, "Ukraine", "Ukrayna", "Ukraine"),
            new Entry("URY", MapContinent.SouthAmerica, 4, 3f, 8f, -55.9669f, -32.9611f, "Uruguay", "Uruguay", "Uruguay"),
            new Entry("USA", MapContinent.NorthAmerica, 2, 1.7f, 5.7f, -97.4826f, 39.5385f, "United States of America", "Amerika Birleşik Devletleri", "Vereinigte Staaten"),
            new Entry("UZB", MapContinent.Asia, 3, 3f, 8f, 64.0054f, 41.6936f, "Uzbekistan", "Özbekistan", "Usbekistan"),
            new Entry("VAT", MapContinent.Europe, 6, 5f, 10f, 12.4534f, 41.9033f, "Vatican", "Vatikan", "Vatikanstadt"),
            new Entry("VCT", MapContinent.NorthAmerica, 6, 5f, 10f, -61.3359f, 13.0879f, "St. Vin. and Gren.", "Saint Vincent ve Grenadinler", "St. Vincent und die Grenadinen"),
            new Entry("VEN", MapContinent.SouthAmerica, 3, 2.5f, 7.5f, -64.5994f, 7.1825f, "Venezuela", "Venezuela", "Venezuela"),
            new Entry("VGB", MapContinent.NorthAmerica, 6, 5f, 9.5f, -64.6366f, 18.4266f, "British Virgin Is.", "Britanya Virjin Adaları", "Britische Jungferninseln"),
            new Entry("VIR", MapContinent.NorthAmerica, 6, 5f, 10f, -64.7792f, 17.7467f, "U.S. Virgin Is.", "ABD Virjin Adaları", "Amerikanische Jungferninseln"),
            new Entry("VNM", MapContinent.Asia, 2, 2f, 7f, 105.3873f, 21.7154f, "Vietnam", "Vietnam", "Vietnam"),
            new Entry("VUT", MapContinent.Oceania, 4, 4f, 9f, 166.9088f, -15.3715f, "Vanuatu", "Vanuatu", "Vanuatu"),
            new Entry("WLF", MapContinent.Oceania, 4, 4.7f, 9f, -178.1374f, -14.2864f, "Wallis and Futuna Is.", "Wallis ve Futuna Adaları", "Wallis und Futuna"),
            new Entry("WSM", MapContinent.Oceania, 4, 3f, 8f, -172.4382f, -13.6391f, "Samoa", "Samoa", "Samoa"),
            new Entry("YEM", MapContinent.Asia, 3, 3f, 8f, 45.8744f, 15.3282f, "Yemen", "Yemen", "Jemen"),
            new Entry("ZAF", MapContinent.Africa, 2, 1.7f, 6.7f, 23.6657f, -29.7088f, "South Africa", "Güney Afrika Cumhuriyeti", "Südafrika"),
            new Entry("ZMB", MapContinent.Africa, 3, 3f, 8f, 26.3953f, -14.6608f, "Zambia", "Zambiya", "Sambia"),
            new Entry("ZWE", MapContinent.Africa, 3, 2.5f, 8f, 29.9254f, -18.9116f, "Zimbabwe", "Zimbabve", "Simbabwe"),
        };

        public static int IndexOf(string code)
        {
            if (string.IsNullOrEmpty(code))
                return -1;

            for (var i = 0; i < All.Length; i++)
            {
                if (All[i].Code == code)
                    return i;
            }

            return -1;
        }
    }
}
