using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace LogistiCore.Backend
{
    public static class ClientValue
    {
        public static bool TryMap(object value, out IDictionary map)
        {
            map = value as IDictionary;
            return map != null && !(value is string);
        }

        public static bool TryList(object value, out IList list)
        {
            list = value as IList;
            return list != null && !(value is string);
        }

        public static bool TryString(object value, out string text)
        {
            text = value as string;
            return text != null;
        }

        public static bool TryInt(object value, out int number)
        {
            if (value is int i)
            {
                number = i;
                return true;
            }

            if (value is long l && l >= int.MinValue && l <= int.MaxValue)
            {
                number = (int)l;
                return true;
            }

            number = 0;
            return false;
        }

        public static bool TryBool(object value, out bool flag)
        {
            if (value is bool b)
            {
                flag = b;
                return true;
            }

            flag = false;
            return false;
        }

        public static object Get(IDictionary map, string key)
        {
            return map != null && map.Contains(key) ? map[key] : null;
        }

        public static Dictionary<string, object> ReadRequest(int? limit = null, string after = null)
        {
            var payload = new Dictionary<string, object> { { "protocolVersion", ClientProtocol.Version } };
            if (limit.HasValue)
                payload["limit"] = limit.Value;
            if (!string.IsNullOrEmpty(after))
                payload["after"] = after;
            return payload;
        }

        public static bool IsSafeIdempotencyKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 128)
                return false;
            if (key.Length >= 4 && key[0] == '_' && key[1] == '_' && key[key.Length - 2] == '_' && key[key.Length - 1] == '_')
                return false;
            for (var i = 0; i < key.Length; i++)
            {
                var c = key[i];
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    return false;
            }

            return true;
        }

        public static bool IsSafeContractId(string contractId)
        {
            if (string.IsNullOrEmpty(contractId) || contractId.Length > 64)
                return false;
            var first = contractId[0];
            if (!(first >= 'A' && first <= 'Z') && !(first >= 'a' && first <= 'z') && !(first >= '0' && first <= '9'))
                return false;
            for (var i = 1; i < contractId.Length; i++)
            {
                var c = contractId[i];
                if (!(c >= 'A' && c <= 'Z') && !(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    return false;
            }

            return true;
        }

        public static string NewIdempotencyKey()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static Dictionary<string, object> AcceptContractPayload(string contractId)
        {
            return new Dictionary<string, object> { { "contractId", contractId } };
        }

        public static Dictionary<string, object> CommandRequest(string idempotencyKey, IReadOnlyDictionary<string, object> payload)
        {
            return new Dictionary<string, object>
            {
                { "protocolVersion", ClientProtocol.Version },
                { "idempotencyKey", idempotencyKey },
                { "payload", payload }
            };
        }

        public static bool TryValidateAcceptContractRequest(IReadOnlyDictionary<string, object> request)
        {
            if (request == null || request.Count != 3)
                return false;
            if (!request.TryGetValue("protocolVersion", out var versionRaw)
                || !TryInt(versionRaw, out var version)
                || !ClientProtocol.IsSupportedVersion(version))
                return false;
            if (!request.TryGetValue("idempotencyKey", out var keyRaw)
                || !TryString(keyRaw, out var key)
                || !IsSafeIdempotencyKey(key))
                return false;
            if (!request.TryGetValue("payload", out var payloadRaw)
                || !TryMap(payloadRaw, out var payload)
                || payload.Count != 1)
                return false;
            return TryString(Get(payload, "contractId"), out var contractId) && IsSafeContractId(contractId);
        }
    }

    public readonly struct ClientEnvelope
    {
        public ClientEnvelope(int protocolVersion, string serverTime, object result)
        {
            ProtocolVersion = protocolVersion;
            ServerTime = serverTime ?? "";
            Result = result;
        }

        public int ProtocolVersion { get; }
        public string ServerTime { get; }
        public object Result { get; }

        public static bool TryParse(object raw, out ClientEnvelope envelope, out ClientCallableFault fault)
        {
            envelope = default;
            fault = ClientCallableFault.MalformedResponse;
            if (!ClientValue.TryMap(raw, out var map))
                return false;
            if (!ClientValue.TryInt(ClientValue.Get(map, "protocolVersion"), out var version))
            {
                fault = ClientCallableFault.ProtocolMismatch;
                return false;
            }

            if (!ClientProtocol.IsSupportedVersion(version))
            {
                fault = ClientCallableFault.ProtocolMismatch;
                return false;
            }

            if (!ClientValue.TryString(ClientValue.Get(map, "serverTime"), out var serverTime) ||
                string.IsNullOrEmpty(serverTime))
                return false;
            if (!map.Contains("result"))
                return false;

            envelope = new ClientEnvelope(version, serverTime, ClientValue.Get(map, "result"));
            fault = ClientCallableFault.None;
            return true;
        }
    }

    public static class ClientMoney
    {
        public static bool TryValidate(string minorUnits, string currency, int scale)
        {
            if (string.IsNullOrEmpty(minorUnits) || scale < 0 || scale > 6)
                return false;
            if (string.IsNullOrEmpty(currency) || currency.Length != 3)
                return false;
            for (var i = 0; i < currency.Length; i++)
            {
                if (!char.IsLetter(currency[i]))
                    return false;
            }

            for (var i = 0; i < minorUnits.Length; i++)
            {
                if (minorUnits[i] < '0' || minorUnits[i] > '9')
                    return false;
            }

            return true;
        }

        public static bool TryFormat(string minorUnits, string currency, int scale, out string display)
        {
            display = "";
            if (!TryValidate(minorUnits, currency, scale))
                return false;
            var code = currency.ToUpperInvariant();
            var digits = minorUnits.TrimStart('0');
            if (digits.Length == 0)
                digits = "0";
            if (scale == 0)
            {
                display = digits + " " + code;
                return true;
            }

            if (digits.Length <= scale)
                digits = new string('0', scale - digits.Length + 1) + digits;
            var split = digits.Length - scale;
            display = digits.Substring(0, split) + "." + digits.Substring(split) + " " + code;
            return true;
        }
    }

    public static class ClientIsoTime
    {
        public static bool TryParse(string value, out DateTime utc)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out utc);
        }
    }
}
