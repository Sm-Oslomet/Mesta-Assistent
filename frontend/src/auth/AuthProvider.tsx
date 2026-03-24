import { useEffect } from "react";
import { useMsal } from "@azure/msal-react";
import { InteractionStatus } from "@azure/msal-browser";
import { loginRequest } from "./msalConfig";

export default function AuthProvider ( {children }: { children: React.ReactNode }) {
    const {instance, accounts, inProgress } = useMsal();

    useEffect(() => {
        if (accounts.length === 0 && inProgress === InteractionStatus.None) {
            instance.loginRedirect(loginRequest);
        }

        if (accounts.length > 0 ) {
            instance.setActiveAccount(accounts[0]);
        }
    }, [accounts, inProgress, instance]);
    return <>{children}</>
}

// written by sm-dev