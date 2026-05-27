namespace EndToEndTests.Fixtures;

/// <summary>
/// Test RSA keypair for JWT signing/validation. Same shape as the dev keys
/// baked into appsettings.json — deliberately a different key so the test
/// suite never accidentally validates a token signed by another environment.
/// </summary>
internal static class TestKeys
{
    public const string KeyId = "tier4-e2e-tests";

    public const string PrivateKeyPem = """
-----BEGIN RSA PRIVATE KEY-----
MIIEpQIBAAKCAQEArTN4vzsRAPTUEgjgucoz5/hBanbMJ0hZCgXNAYqZxhncRJoy
0V+IINFl5JSTeA/EOML7rx2vU5J1fpG/re8hLYTK1CQj46YMiVYSmMFcoxqz6c9q
CGgeVVr6LFc07EUHIfcRyz6iQw8EW1KhCTtU49gWraXkTX/jArSzkHiKUk2zvm69
BsI5hdYagKLCQ912XZ8+m0oHU4WkjJWcZIZywwjSzyFaM1D0EB/8/4B2y8xqNimN
MmfYpaa9Urcf4yhtJ/Go4xZtF87pT2fK+A6KmOH/ySBmvscGln5Q5aLegzUpLo86
w4tRVoV4UXrL/p63UwdSj9OoOg/rArPS5NGk0QIDAQABAoIBAHeQwthwtCpO2V+h
1VEsr2yBytbuL70miqEKpB1eSw2gqJiLQm2bX2QYahjEIJGPgMwWfpzDB1fQEWBQ
yVwPan373/FXCZeL97ePPcNKKONH+c98qhwnlFkkNvQJN7WraWMfJp+CG43jfgR1
JEo1NUMc13sEvHhrwpEJobQoIoTxj8qYSX46ckbu9O1MiSdYQpBMsMW1+MSW9uE9
/I+OY/M/SXEb3Dh9Id3vddSw1benOHI7e29y4x/loEgYX6ZChqVLXflKriv/UN8E
zWF346wP1kOiubk1P0rPovFpqFpNohw8KnlsOhNwchkwRNkhF1+xTcdhxq7tB2TF
wyHhvAECgYEA4CCvN9yigTYOvoJkpbcikvvJngkN4UwClAHVPyZitSuf1TA3Pf6q
xspRZIq/mULp6pmgJFdSQnBOYO1U81KlqW1jw2R9owKy5Wo0JBiX8Yon7vdSTxLr
7IX7VsQK2UiJW7gzUmTQ8YkFad5TKlm0FNgoK0t4+BGULlRj8ITuDLECgYEAxdTQ
2ItwPKqfvVb+MOXx0jBIK8dDBsqd/ZXCBIJl21h+DC7y6D9qSSPk3lk3LnKqEl9S
7oxw9NA5aHvsT1CusRRXtxdnzS3xEAZw/6QqsFkX0xPShWC9b/WmvWvdBWBj9eA0
X6n7R5M5+S/5utPnnDefa2QW41oWR3BSn546oiECgYEAvVA+UKQoC5Zau+auFx/q
r1bIxZmGROCMaPJnarEEvV0847mXX+FF5SYtvAxKrK1NomDjWO79R6tPOSYfFGyi
C8ufcgLm8JMuAwRDSJ4Rce+trXbw6mPkLeQ6Gd77/u77PyMHDrijmPGRRgyKGQKu
TtEKlQ9p/bfzf3K+/AF8hfECgYEAk/qSlcgHlnmSr1BpJy55al4PPh/49RWOhGcH
D9RyWFajQn3D2RHGcRtWUTOu4SGIMeH36NRIkfdHWe6IXvPdGDw9OIlbbdDVpsUK
tU6ZV/vspEkJihdI3HyF0t7iHulxHDQvOPevLGTmUo0eYi+r6eB5cR0XOczjKWDN
jPQQq8ECgYEAxEdcN+2r1PvuRu/M9tvu+/iNO2jLTZMKuvhkyDr3VLhv4B/B+bwF
rmaTx2+ydBKqjSC5+J9f2Qf1QjW8mTfonjkIL+Fmds5nWt0/nfEgFz/1YCVB7ceq
KM9QDjzNxa2ajBMa1nEzllZGjiHjKtNWHLd4AM7HtKSnIl9DA80BZPM=
-----END RSA PRIVATE KEY-----
""";

    public const string PublicKeyPem = """
-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEArTN4vzsRAPTUEgjgucoz
5/hBanbMJ0hZCgXNAYqZxhncRJoy0V+IINFl5JSTeA/EOML7rx2vU5J1fpG/re8h
LYTK1CQj46YMiVYSmMFcoxqz6c9qCGgeVVr6LFc07EUHIfcRyz6iQw8EW1KhCTtU
49gWraXkTX/jArSzkHiKUk2zvm69BsI5hdYagKLCQ912XZ8+m0oHU4WkjJWcZIZy
wwjSzyFaM1D0EB/8/4B2y8xqNimNMmfYpaa9Urcf4yhtJ/Go4xZtF87pT2fK+A6K
mOH/ySBmvscGln5Q5aLegzUpLo86w4tRVoV4UXrL/p63UwdSj9OoOg/rArPS5NGk
0QIDAQAB
-----END PUBLIC KEY-----
""";
}
