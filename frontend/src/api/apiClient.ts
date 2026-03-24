import {msalInstance, loginRequest} from "../auth/msalConfig";

export async function callApi(url: string, options: RequestInit = {}) {
    const account = msalInstance.getActiveAccount();

    if(!account){
        throw new Error("No active account");
    }

    const tokenResponse = await msalInstance.acquireTokenSilent({
        scopes: loginRequest.scopes,
        account
    });

    const accessToken = tokenResponse.accessToken;

    return fetch(url, {
        ...options,
        headers: {
            ...(options.headers || {}),
            Authorization: `Bearer ${accessToken}`,
            "Content-Type": "application/json"
        }
    });
}

// written by Sm-Dev