using System;

namespace LOSTARK_GEM_FINDER.Core
{
    public class ApiKeyInvalidException : Exception
    {
        public ApiKeyInvalidException() : base("API 키가 유효하지 않습니다. 키를 다시 확인해 주세요.") { }
    }

    public class ApiRateLimitException : Exception
    {
        public ApiRateLimitException() : base("API 호출 한도를 초과했습니다. 잠시 후 다시 시도해 주세요.") { }
    }

    public class ApiMaintenanceException : Exception
    {
        public ApiMaintenanceException() : base("로스트아크 API 서버 점검 중입니다. 점검이 끝난 뒤 다시 시도해 주세요.") { }
    }
}