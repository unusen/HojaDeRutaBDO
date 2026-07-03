window.hojaUpsertProgress = (function () {
    let pollTimer = null;
    let activeContext = null;
    const INITIAL_PROGRESS_GRACE_MS = 6000;
    const NETWORK_RECOVERY_GRACE_MS = 12000;
    const STALLED_OPERATION_MS = 20000;

    function getElements() {
        return {
            overlay: document.getElementById("operationProgressOverlay"),
            title: document.getElementById("operationProgressTitle"),
            message: document.getElementById("operationProgressMessage"),
            steps: document.getElementById("operationProgressSteps"),
            closeButton: document.getElementById("operationProgressClose")
        };
    }

    function init() {
        const elements = getElements();
        if (!elements.overlay || !elements.closeButton) {
            return;
        }

        elements.closeButton.addEventListener("click", handleManualClose);
    }

    function handleManualClose() {
        if (!activeContext) {
            return;
        }

        const payload = activeContext.executionError || activeContext.stalledWarning;
        if (!payload) {
            return;
        }

        const restoreUi = activeContext.restoreUi;

        hideOverlay();
        if (typeof restoreUi === "function") {
            restoreUi();
        }

        if (activeContext.executionError && typeof activeContext.onExecutionErrorClosed === "function") {
            activeContext.onExecutionErrorClosed(payload);
        } else if (activeContext.stalledWarning && typeof activeContext.onStalledClosed === "function") {
            activeContext.onStalledClosed(payload);
        } else if (typeof window.mostrarErrorEnAlert === "function") {
            window.mostrarErrorEnAlert(payload.message, payload.errors || []);
        }

        clearActiveContext();
    }

    function clearActiveContext() {
        stopPolling();
        if (activeContext && activeContext.animationHandle) {
            window.clearTimeout(activeContext.animationHandle);
        }
        activeContext = null;
    }

    function stopPolling() {
        if (pollTimer) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    function generateOperationId() {
        if (window.crypto && typeof window.crypto.randomUUID === "function") {
            return window.crypto.randomUUID();
        }

        return "op-" + Date.now() + "-" + Math.random().toString(16).slice(2);
    }

    function showOverlay(title, message) {
        const elements = getElements();
        if (!elements.overlay || !elements.title || !elements.message || !elements.steps || !elements.closeButton) {
            return;
        }

        elements.title.textContent = title || "Procesando";
        elements.message.textContent = message || "Preparando operación...";
        elements.steps.innerHTML = "";
        elements.closeButton.textContent = "Cerrar";
        elements.closeButton.classList.add("d-none");
        elements.overlay.classList.remove("d-none");
        document.body.classList.add("operation-progress-open");
    }

    function hideOverlay() {
        const elements = getElements();
        if (!elements.overlay) {
            return;
        }

        elements.overlay.classList.add("d-none");
        document.body.classList.remove("operation-progress-open");
    }

    function getStepIcon(status) {
        switch (status) {
            case "running":
                return '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
            case "completed":
                return '<i class="bi bi-check-lg"></i>';
            case "failed":
                return '<i class="bi bi-x-lg"></i>';
            default:
                return '<span class="operation-progress-step-dot"></span>';
        }
    }

    function normalizeSnapshotSteps(snapshot) {
        const steps = (snapshot.steps || []).map(function (step) {
            return {
                label: step.label,
                status: step.status || "pending",
                detail: step.detail || ""
            };
        });

        if (snapshot.status === "completed" && snapshot.redirectUrl && steps.length > 0) {
            const redirectIndex = steps.length - 1;
            steps[redirectIndex] = {
                label: "Redireccionando",
                status: "running",
                detail: "Aguarde por favor..."
            };
        }

        return steps;
    }

    function renderStepList(steps) {
        const elements = getElements();
        if (!elements.steps) {
            return;
        }

        elements.steps.innerHTML = "";

        steps.forEach(function (step) {
            const item = document.createElement("li");
            item.className = "operation-progress-step";
            item.dataset.status = step.status || "pending";

            const icon = document.createElement("span");
            icon.className = "operation-progress-step-icon";
            icon.innerHTML = getStepIcon(step.status);

            const body = document.createElement("div");
            body.className = "operation-progress-step-body";

            const label = document.createElement("div");
            label.className = "operation-progress-step-label";
            label.textContent = step.label;
            body.appendChild(label);

            if (step.detail) {
                const detail = document.createElement("div");
                detail.className = "operation-progress-step-detail";
                detail.textContent = step.detail;
                body.appendChild(detail);
            }

            item.appendChild(icon);
            item.appendChild(body);
            elements.steps.appendChild(item);
        });
    }

    function queueSnapshotRender(snapshot) {
        if (!activeContext) {
            return;
        }

        const elements = getElements();
        if (!elements.overlay || !elements.title || !elements.message || !elements.steps || !elements.closeButton) {
            return;
        }

        elements.title.textContent = snapshot.title || "Procesando";
        elements.message.textContent = snapshot.message || "Procesando operación...";

        const normalizedSteps = normalizeSnapshotSteps(snapshot);
        const currentSteps = activeContext.displayedSteps || [];

        const needsReset =
            currentSteps.length !== normalizedSteps.length ||
            currentSteps.some(function (step, index) {
                return !normalizedSteps[index] || normalizedSteps[index].label !== step.label;
            });

        if (needsReset) {
            activeContext.displayedSteps = normalizedSteps.map(function (step) {
                return {
                    label: step.label,
                    status: "pending",
                    detail: ""
                };
            });
            renderStepList(activeContext.displayedSteps);
        }

        const updates = [];
        const displayed = activeContext.displayedSteps || [];

        normalizedSteps.forEach(function (step, index) {
            const current = displayed[index];
            if (!current) {
                return;
            }

            if (current.label !== step.label || current.status !== step.status || current.detail !== step.detail) {
                updates.push({
                    index: index,
                    step: step
                });
            }
        });

        if (activeContext.animationHandle) {
            window.clearTimeout(activeContext.animationHandle);
            activeContext.animationHandle = null;
        }

        function applyNextUpdate(updateIndex) {
            if (!activeContext) {
                return;
            }

            if (updateIndex >= updates.length) {
                renderStepList(activeContext.displayedSteps || normalizedSteps);
                return;
            }

            const update = updates[updateIndex];
            activeContext.displayedSteps[update.index] = {
                label: update.step.label,
                status: update.step.status,
                detail: update.step.detail
            };
            renderStepList(activeContext.displayedSteps);

            activeContext.animationHandle = window.setTimeout(function () {
                applyNextUpdate(updateIndex + 1);
            }, 110);
        }

        if (updates.length === 0) {
            renderStepList(activeContext.displayedSteps || normalizedSteps);
        } else {
            applyNextUpdate(0);
        }

        if (snapshot.status === "failed") {
            elements.closeButton.classList.remove("d-none");
        } else {
            elements.closeButton.classList.add("d-none");
        }
    }

    function renderSnapshot(snapshot) {
        queueSnapshotRender(snapshot);
    }

    async function fetchSnapshot(operationId, progressUrl) {
        const response = await fetch(progressUrl + "?operationId=" + encodeURIComponent(operationId), {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        if (!response.ok) {
            const error = new Error("No pudimos recuperar el progreso de la operación.");
            error.status = response.status;
            throw error;
        }

        return await response.json();
    }

    function collectFailedStepDetails(steps) {
        return steps
            .filter(function (step) { return step.status === "failed" && step.detail; })
            .map(function (step) { return step.detail; });
    }

    function renderExecutionFailureFallback(message) {
        const elements = getElements();
        if (!elements.message || !elements.closeButton) {
            return;
        }

        elements.message.textContent = message;
        elements.closeButton.classList.remove("d-none");
        elements.closeButton.textContent = "Cerrar";
    }

    function renderStalledState(message) {
        const elements = getElements();
        if (!elements.message || !elements.closeButton) {
            return;
        }

        elements.message.textContent = message;
        elements.closeButton.classList.remove("d-none");
        elements.closeButton.textContent = "Cerrar y seguir aquí";
    }

    function markSnapshotSeen(snapshot) {
        if (!activeContext) {
            return;
        }

        const serialized = JSON.stringify(snapshot.steps || []);
        if (activeContext.lastSnapshotSignature !== serialized || activeContext.lastStatus !== snapshot.status) {
            activeContext.lastUpdateAt = Date.now();
            activeContext.lastSnapshotSignature = serialized;
            activeContext.lastStatus = snapshot.status;
        }

        activeContext.hadSnapshot = true;
        activeContext.consecutivePollErrors = 0;
        activeContext.consecutiveNotFound = 0;
    }

    function maybeEnableStalledEscape() {
        if (!activeContext || activeContext.executionError || activeContext.stalledWarning || !activeContext.hadSnapshot) {
            return false;
        }

        const now = Date.now();
        const lastUpdateAt = activeContext.lastUpdateAt || activeContext.startedAt;
        if (now - lastUpdateAt < STALLED_OPERATION_MS) {
            return false;
        }

        activeContext.stalledWarning = {
            message: "La operación está tardando más de lo habitual. Puede cerrar esta ventana y verificar el estado en unos instantes.",
            errors: []
        };
        renderStalledState("La operación sigue en curso y está tardando más de lo habitual. Puede seguir esperando o cerrar esta ventana.");
        return true;
    }

    function startPolling(operationId, progressUrl) {
        stopPolling();

        pollTimer = window.setInterval(async function () {
            try {
                const snapshot = await fetchSnapshot(operationId, progressUrl);
                markSnapshotSeen(snapshot);
                queueSnapshotRender(snapshot);

                if (snapshot.status === "completed") {
                    stopPolling();
                    if (activeContext && typeof activeContext.restoreUi === "function") {
                        activeContext.restoreUi();
                    }
                    window.setTimeout(function () {
                        if (snapshot.redirectUrl) {
                            window.location.href = snapshot.redirectUrl;
                            return;
                        }

                        hideOverlay();
                        if (activeContext && typeof activeContext.restoreUi === "function") {
                            activeContext.restoreUi();
                        }
                        clearActiveContext();
                    }, 700);
                }

                if (snapshot.status === "failed") {
                    stopPolling();
                    if (activeContext) {
                        activeContext.executionError = {
                            message: snapshot.message || "La operación falló.",
                            errors: collectFailedStepDetails(snapshot.steps || [])
                        };
                    }
                }
            } catch (error) {
                if (!activeContext) {
                    stopPolling();
                    return;
                }

                const now = Date.now();
                const statusCode = error && typeof error.status === "number" ? error.status : null;
                const isNotFound = statusCode === 404;

                if (!activeContext.hadSnapshot && isNotFound && now - activeContext.startedAt < INITIAL_PROGRESS_GRACE_MS) {
                    return;
                }

                activeContext.consecutivePollErrors = (activeContext.consecutivePollErrors || 0) + 1;
                activeContext.consecutiveNotFound = isNotFound
                    ? (activeContext.consecutiveNotFound || 0) + 1
                    : 0;

                if (maybeEnableStalledEscape()) {
                    return;
                }

                const lastHealthyAt = activeContext.lastUpdateAt || activeContext.startedAt;
                const shouldKeepTrying = activeContext.hadSnapshot
                    ? now - lastHealthyAt < NETWORK_RECOVERY_GRACE_MS
                    : isNotFound && now - activeContext.startedAt < NETWORK_RECOVERY_GRACE_MS;

                if (shouldKeepTrying) {
                    return;
                }

                stopPolling();
                activeContext.executionError = {
                    message: "No pudimos seguir el progreso de la operación. Intentá nuevamente.",
                    errors: []
                };
                renderExecutionFailureFallback(activeContext.executionError.message);
            }
        }, 700);
    }

    async function runFormOperation(options) {
        const form = options.form;
        if (!form) {
            return null;
        }

        const operationId = generateOperationId();
        const progressUrl = options.progressUrl || "/Home/GetOperationProgress";
        const formData = new FormData(form);

        if (typeof options.prepareFormData === "function") {
            const shouldContinue = options.prepareFormData(formData);
            if (shouldContinue === false) {
                return null;
            }
        }

        formData.set("operationId", operationId);

        const restoreUi = function () {
            (options.disableElements || []).forEach(function (element) {
                if (element) {
                    element.disabled = false;
                }
            });
        };

        (options.disableElements || []).forEach(function (element) {
            if (element) {
                element.disabled = true;
            }
        });

        if (typeof options.beforeOpenOverlay === "function") {
            options.beforeOpenOverlay();
        }

        activeContext = {
            executionError: null,
            stalledWarning: null,
            restoreUi: restoreUi,
            onExecutionErrorClosed: options.onExecutionErrorClosed,
            onStalledClosed: options.onStalledClosed,
            startedAt: Date.now(),
            lastUpdateAt: null,
            hadSnapshot: false,
            consecutivePollErrors: 0,
            consecutiveNotFound: 0,
            lastSnapshotSignature: null,
            lastStatus: null,
            displayedSteps: null,
            animationHandle: null
        };

        showOverlay(options.initialTitle, options.initialMessage);
        startPolling(operationId, progressUrl);

        try {
            const response = await fetch(options.url || form.action, {
                method: options.method || "POST",
                body: formData,
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                }
            });

            const contentType = response.headers.get("content-type") || "";
            const maxUploadLabel = options.maxUploadLabel
                || window.hojaUploadSettings?.maxFileSizeLabel
                || "60 MB";
            let data = null;

            if (contentType.toLowerCase().includes("application/json")) {
                data = await response.json();
            } else if (response.status === 413) {
                data = {
                    success: false,
                    errorPhase: "preflight",
                    message: "No se puede guardar la hoja porque el archivo adjunto supera el tamaño permitido.",
                    errors: [
                        `El archivo adjunto supera el máximo permitido de ${maxUploadLabel}.`
                    ]
                };
            } else {
                const fallbackText = await response.text();
                throw new Error(`Respuesta inesperada del servidor. Status=${response.status}. BodyLength=${fallbackText ? fallbackText.length : 0}`);
            }

            if (data.success) {
                return data;
            }

            stopPolling();

            if (data.errorPhase === "execution") {
                try {
                    const snapshot = await fetchSnapshot(operationId, progressUrl);
                    renderSnapshot(snapshot);
                } catch (snapshotError) {
                    renderExecutionFailureFallback(data.message || "La operación falló.");
                }

                if (activeContext) {
                    activeContext.executionError = {
                        message: data.message || "La operación falló.",
                        errors: data.errors || []
                    };
                }

                return data;
            }

            hideOverlay();
            restoreUi();
            clearActiveContext();

            if (typeof options.onPreflightError === "function") {
                const handled = options.onPreflightError(data);
                if (handled === true) {
                    return data;
                }
            }

            if (typeof window.mostrarErrorEnAlert === "function") {
                window.mostrarErrorEnAlert(data.message || "No pudimos procesar la solicitud.", data.errors || []);
            }

            return data;
        } catch (error) {
            stopPolling();
            hideOverlay();
            restoreUi();
            clearActiveContext();

            if (typeof window.mostrarErrorEnAlert === "function") {
                window.mostrarErrorEnAlert("No pudimos procesar la solicitud. Intentá nuevamente en unos instantes.");
            }

            return null;
        }
    }

    return {
        init: init,
        runFormOperation: runFormOperation,
        hideOverlay: hideOverlay
    };
})();

window.hojaNotificationStatus = (function () {
    let pollTimer = null;
    let activeOptions = null;
    let retryInFlight = false;

    function getElements() {
        return {
            panel: document.getElementById("notificationStatusPanel"),
            summary: document.getElementById("notificationStatusSummary"),
            list: document.getElementById("notificationStatusList"),
            antiForgeryToken: document.querySelector("#notificationStatusPanel input[name='__RequestVerificationToken']")
        };
    }

    function init(options) {
        const elements = getElements();
        if (!elements.panel || !options || !options.hojaId) {
            return;
        }

        activeOptions = options;
        pollNow();
        startPolling();
    }

    function startPolling() {
        stopPolling();
        pollTimer = window.setInterval(pollNow, 4000);
    }

    function stopPolling() {
        if (pollTimer) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    async function pollNow() {
        if (!activeOptions) {
            stopPolling();
            return;
        }

        try {
            const response = await fetch(
                activeOptions.endpoint + "?hojaId=" + encodeURIComponent(activeOptions.hojaId),
                {
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });

            if (!response.ok) {
                return;
            }

            const statuses = await response.json();
            render(statuses || []);

            const hasOpenStatuses = (statuses || []).some(function (status) {
                return status.status === "pending" || status.status === "processing";
            });

            if (!hasOpenStatuses) {
                stopPolling();
            }
        } catch (error) {
            // El polling es informativo; no bloqueamos la pantalla por este error.
        }
    }

    function render(statuses) {
        const elements = getElements();
        if (!elements.panel || !elements.summary || !elements.list) {
            return;
        }

        if (!statuses.length) {
            elements.panel.classList.add("d-none");
            elements.list.innerHTML = "";
            elements.summary.textContent = "";
            return;
        }

        elements.panel.classList.remove("d-none");
        elements.list.innerHTML = "";

        const summaryText = buildSummary(statuses);
        elements.summary.textContent = summaryText;

        statuses.forEach(function (status) {
            const item = document.createElement("li");
            item.className = "notification-status-item";
            item.dataset.status = status.status || "pending";

            const icon = document.createElement("span");
            icon.className = "notification-status-icon";
            icon.innerHTML = getStatusIcon(status.status);

            const content = document.createElement("div");
            content.className = "notification-status-content";

            const title = document.createElement("div");
            title.className = "notification-status-item-title";
            title.textContent = status.title || "Notificación";
            content.appendChild(title);

            const detail = document.createElement("div");
            detail.className = "notification-status-item-detail";
            detail.textContent = buildDetail(status);
            content.appendChild(detail);

            item.appendChild(icon);
            item.appendChild(content);

            if (status.status === "failed" && status.jobId) {
                item.appendChild(buildRetryButton(status));
            }

            elements.list.appendChild(item);
        });
    }

    function buildRetryButton(status) {
        const actions = document.createElement("div");
        actions.className = "notification-status-actions";

        const button = document.createElement("button");
        button.type = "button";
        button.className = "btn btn-sm btn-outline-danger notification-status-retry";
        button.textContent = "Reintentar";
        button.disabled = retryInFlight;
        button.addEventListener("click", function () {
            retryStatus(status, button);
        });

        actions.appendChild(button);
        return actions;
    }

    function buildSummary(statuses) {
        const processing = statuses.filter(function (status) {
            return status.status === "pending" || status.status === "processing";
        }).length;
        const failed = statuses.filter(function (status) {
            return status.status === "failed";
        }).length;

        if (processing > 0) {
            return processing === 1 ? "1 envío en curso" : processing + " envíos en curso";
        }

        if (failed > 0) {
            return failed === 1 ? "1 envío con error" : failed + " envíos con error";
        }

        return "Todos los envíos recientes fueron procesados";
    }

    function buildDetail(status) {
        const recipients = Array.isArray(status.recipients) && status.recipients.length
            ? " a " + status.recipients.join(", ")
            : "";
        const completedAt = formatStatusDate(status.sentAtUtc || status.updatedAtUtc);
        const updatedAt = formatStatusDate(status.updatedAtUtc || status.createdAtUtc);

        switch (status.status) {
            case "processing":
                return (updatedAt ? updatedAt + " hs - " : "") + "Enviando email" + recipients;
            case "completed":
                return (completedAt ? completedAt + " hs - " : "") + "Email enviado" + recipients;
            case "failed":
                return (updatedAt ? updatedAt + " hs - " : "") + (status.lastError || "No pudimos enviar el email luego de varios intentos.");
            default:
                return (updatedAt ? updatedAt + " hs - " : "") + "Pendiente de envio" + recipients;
        }
    }

    function formatStatusDate(value) {
        if (!value) {
            return "";
        }

        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return "";
        }

        const day = String(date.getDate()).padStart(2, "0");
        const month = String(date.getMonth() + 1).padStart(2, "0");
        const year = date.getFullYear();
        const hours = String(date.getHours()).padStart(2, "0");
        const minutes = String(date.getMinutes()).padStart(2, "0");

        return day + "-" + month + "-" + year + " " + hours + ":" + minutes;
    }

    function getStatusIcon(status) {
        switch (status) {
            case "processing":
                return '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>';
            case "completed":
                return '<i class="bi bi-check-lg"></i>';
            case "failed":
                return '<i class="bi bi-exclamation-lg"></i>';
            default:
                return '<i class="bi bi-clock-history"></i>';
        }
    }

    async function retryStatus(status, button) {
        const elements = getElements();
        if (!activeOptions || !activeOptions.hojaId || !status || !status.jobId || retryInFlight) {
            return;
        }

        const token = elements.antiForgeryToken ? elements.antiForgeryToken.value : "";
        if (!token) {
            if (typeof window.mostrarErrorEnAlert === "function") {
                window.mostrarErrorEnAlert("No pudimos validar el reintento del email. Recarga la pantalla e intenta nuevamente.");
            }
            return;
        }

        retryInFlight = true;
        if (button) {
            button.disabled = true;
            button.textContent = "Reintentando...";
        }

        try {
            const body = new URLSearchParams();
            body.append("hojaId", activeOptions.hojaId);
            body.append("jobId", status.jobId);
            body.append("__RequestVerificationToken", token);

            const response = await fetch("/Home/RetryNotification", {
                method: "POST",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: body.toString()
            });

            const payload = await response.json().catch(function () { return null; });
            if (!response.ok || !payload || payload.success === false) {
                throw new Error(payload && payload.message
                    ? payload.message
                    : "No pudimos reintentar el email en este momento.");
            }

            startPolling();
            await pollNow();
        } catch (error) {
            if (typeof window.mostrarErrorEnAlert === "function") {
                window.mostrarErrorEnAlert(error.message || "No pudimos reintentar el email en este momento.");
            }
        } finally {
            retryInFlight = false;
            await pollNow();
        }
    }

    return {
        init: init
    };
})();
