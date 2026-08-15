// google-auth.js
// Gestiona el flux OAuth2 PKCE amb Google Identity Services.
// Desa el token a sessionStorage per restaurar sessions.

window.googleAuth = {

    tokenClient: null,
    _resolveToken: null,

    signIn: function (clientId) {
        return new Promise((resolve, reject) => {
            window.googleAuth._resolveToken = resolve;

            window.googleAuth.tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: clientId,
                scope: 'https://www.googleapis.com/auth/drive.file',
                callback: (response) => {
                    if (response.error) {
                        reject(new Error(response.error));
                        return;
                    }
                    // Desar token a sessió
                    sessionStorage.setItem('goog_token', response.access_token);
                    resolve(response.access_token);
                }
            });

            window.googleAuth.tokenClient.requestAccessToken({ prompt: 'consent' });
        });
    },

    signOut: function () {
        const token = sessionStorage.getItem('goog_token');
        if (token) {
            google.accounts.oauth2.revoke(token, () => {});
        }
        sessionStorage.removeItem('goog_token');
    },

    getStoredToken: function () {
        return sessionStorage.getItem('goog_token');
    }
};
