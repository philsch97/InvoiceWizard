window.invoiceWizardAuth = {
    getSession: function () {
        return window.localStorage.getItem("invoicewizard.auth");
    },
    setSession: function (value) {
        window.localStorage.setItem("invoicewizard.auth", value);
    },
    clearSession: function () {
        window.localStorage.removeItem("invoicewizard.auth");
    },
    getCredentials: function () {
        return window.localStorage.getItem("invoicewizard.auth.credentials");
    },
    setCredentials: function (value) {
        window.localStorage.setItem("invoicewizard.auth.credentials", value);
    },
    clearCredentials: function () {
        window.localStorage.removeItem("invoicewizard.auth.credentials");
    }
};
