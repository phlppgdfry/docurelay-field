namespace DocuRelay.Mobile;

public static class ApiOptions
{
    // Android emulators resolve the development machine through 10.0.2.2.
    // Set this to the deployed API origin before creating a release build.
    public const string BaseUrl = "http://10.0.2.2:5050/";
}

