// Opprettet av Sm-Oslomet
// lagd for å sette opp autentisering

import { PublicClientApplication } from "@azure/msal-browser";

export const msalConfig = {
    auth: {
        clientId: "c200fe56-57e2-4fc1-86ac-654e4b79b528",
        authority: "https://login.microsoftonline.com/413fb44d-30bd-4e36-b5d4-ee61e6b32030",
        redirectUri: window.location.origin
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false
    }
};

export const loginRequest = {
    scopes: ["api://c200fe56-57e2-4fc1-86ac-654e4b79b528/access_as_user"]
};

export const msalInstance = new PublicClientApplication(msalConfig);

// written by sm-dev