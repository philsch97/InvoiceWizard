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
    },
    getBootstrapState: function () {
        return window.localStorage.getItem("invoicewizard.auth.bootstrap");
    },
    setBootstrapState: function (value) {
        window.localStorage.setItem("invoicewizard.auth.bootstrap", value);
    },
    clearBootstrapState: function () {
        window.localStorage.removeItem("invoicewizard.auth.bootstrap");
    }
};

window.invoiceWizardStorage = {
    getItem: function (key) {
        return window.localStorage.getItem(key);
    },
    setItem: function (key, value) {
        window.localStorage.setItem(key, value);
    },
    removeItem: function (key) {
        window.localStorage.removeItem(key);
    },
    isOnline: function () {
        return window.navigator.onLine;
    }
};
