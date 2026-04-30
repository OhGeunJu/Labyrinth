public enum BuildCategory
{
    Resource,   // 자원
    Defense,    // 방어
    Building,   // 건물
    Misc        // 기타
}

public static class BuildCategoryExt
{
    public static string DisplayName(this BuildCategory c) => c switch
    {
        BuildCategory.Resource => "자원",
        BuildCategory.Defense  => "방어",
        BuildCategory.Building => "건물",
        BuildCategory.Misc     => "기타",
        _                      => c.ToString()
    };
}
