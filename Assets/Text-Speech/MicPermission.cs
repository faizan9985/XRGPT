using UnityEngine;

public class MicPermissionRequester : MonoBehaviour
{
    void Start()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }
    }
}
