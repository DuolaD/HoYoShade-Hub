using Dapper;
using HoYoShadeHub.Core;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;

namespace HoYoShadeHub.Features.Database;


internal class DapperSqlMapper
{

    private static JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive = true };


    public class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override DateTimeOffset Parse(object value)
        {
            if (value is string str)
            {
                return DateTimeOffset.Parse(str);
            }
            else
            {
                return new DateTimeOffset();
            }
        }

        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.Value = value.ToString();
        }
    }


    public class StringListHandler : SqlMapper.TypeHandler<List<string>>
    {
        public override List<string> Parse(object value)
        {
            if (value is string str)
            {
                if (!string.IsNullOrWhiteSpace(str))
                {
                    return JsonSerializer.Deserialize<List<string>>(str)!;
                }
            }
            return new();
        }

        public override void SetValue(IDbDataParameter parameter, List<string>? value)
        {
            parameter.Value = JsonSerializer.Serialize(value, JsonSerializerOptions);
        }
    }


    public class GameBizHandler : SqlMapper.TypeHandler<GameBiz>
    {
        public override GameBiz Parse(object value)
        {
            return new GameBiz(value as string);
        }

        public override void SetValue(IDbDataParameter parameter, GameBiz value)
        {
            parameter.Value = value.Value;
        }
    }


}


