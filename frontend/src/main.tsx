import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";

import { MsalProvider } from "@azure/msal-react";
import { msalInstance } from "./auth/msalConfig";
import AuthProvider from "./auth/AuthProvider";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <MsalProvider instance={msalInstance}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MsalProvider>
  </React.StrictMode>
);

// mostly rewritten by Sm-Dev