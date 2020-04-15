using System;

namespace dFakto.States.Workers.Sql.Common
{
    public class SqlQuery
    {
        public string QueryFileToken { get; set; }
        public string QueryFileName { get; set; }
        public string Query { get; set; }
        public SqlQueryType Type { get; set; } = SqlQueryType.NonQuery;
        public int MaxResults { get; set; } = 10;
        public SqlQueryParameter[] Params { get; set; }
    }
}