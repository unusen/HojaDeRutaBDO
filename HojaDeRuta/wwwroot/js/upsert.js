(() => {
    const configElement = document.getElementById("upsertConfig");
    if (!configElement) {
        return;
    }

    const config = JSON.parse(configElement.textContent);
const revisoresData = config.revisoresData;
        const auditoriaNombreObjetivo = "Informe del auditor";
        window.hojaUploadSettings = config.uploadSettings;
        var codClientePlataforma = "";
        const idHoja = document.getElementById("Id");
        let isInitialLoad = true;
        let oldSocioFirmante = null;

        document.addEventListener("DOMContentLoaded", function () {
            inicializarEventosVista();
            window.hojaUpsertProgress?.init();
            window.hojaNotificationStatus?.init({
                hojaId: idHoja?.value || "",
                endpoint: "/Home/GetNotificationStatuses"
            });
            addClassToLabels('fs-7');
            actualizarCodigoPlataforma(config.cliente);
            inicializarFormulario();
            validateModel();
            inicializarRevisores();
            inicializarNombreGenerico();
            inicializarModalConfirmacion();
            //inicializarBloqueoFormularioConfirmacion();
            inicializarSpinnerDescarga();
            isInitialLoad = false;
            validarGestorFinal();
            inicializarEventosArchivo();
            inicializarValidacionArchivo();
            verificarArchivoAlCargar();
        })

        function inicializarValidacionArchivo() {
            const form = document.getElementById('hojaForm');
            const archivoRutaDoc = document.getElementById('archivoRutaDoc');
            const forceNumeroReassignmentInput = document.getElementById('forceNumeroReassignment');
            const reservedNumeroInput = document.getElementById('reservedNumero');
            const numeroInput = document.getElementById('Numero');

            if (form && archivoRutaDoc) {
                const validarNumeroCreate = async function () {
                    const numeroValue = numeroInput?.value?.trim() || '';
                    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                    const formData = new FormData();
                    formData.set('numero', numeroValue);

                    if (token) {
                        formData.set('__RequestVerificationToken', token);
                    }

                    const response = await fetch(config.validarNumeroCreateUrl, {
                        method: 'POST',
                        body: formData,
                        headers: {
                            'X-Requested-With': 'XMLHttpRequest'
                        }
                    });

                    const contentType = response.headers.get('content-type') || '';
                    if (!contentType.toLowerCase().includes('application/json')) {
                        throw new Error('Respuesta inesperada al validar numeracion.');
                    }

                    return await response.json();
                };

                const ejecutarOperacionUpsert = async function (submitter) {
                    const operationMode = submitter?.dataset.operationMode || 'Update';
                    const initialTitle = operationMode === 'Create' ? 'Creando hoja' : 'Guardando cambios';
                    const initialMessage = operationMode === 'Create'
                        ? 'Estamos preparando la nueva hoja de ruta...'
                        : 'Estamos guardando los cambios realizados...';

                    return await window.hojaUpsertProgress.runFormOperation({
                        form: form,
                        disableElements: [submitter],
                        initialTitle: initialTitle,
                        initialMessage: initialMessage,
                        prepareFormData: function (formData) {
                            const fileInput = document.getElementById('archivoRutaDoc');
                            const hasDirtyFile = fileInput?.dataset.fileDirty === 'true';
                            const hasSelectedFile = !!(fileInput && fileInput.files && fileInput.files.length > 0 && fileInput.files[0] && fileInput.files[0].size > 0);

                            if (!hasDirtyFile || !hasSelectedFile) {
                                formData.delete('archivoDoc');
                            }

                            formData.set('mode', operationMode);
                            return true;
                        },
                        onPreflightError: function (payload) {
                            return false;
                        },
                        onExecutionErrorClosed: function (payload) {
                            mostrarErrorEnAlert(payload.message, payload.errors || []);
                        }
                    });
                };

                form.addEventListener('submit', async function (e) {
                    e.preventDefault();

                    const submitter = e.submitter;
                    const isBackAction = submitter && submitter.innerText.includes('Volver');

                    if (isBackAction) {
                        window.location.href = config.indexUrl;
                        return;
                    }

                    formularioIntentado = true;
                    const esValido = window.actualizarValidaciones();
                    if (!esValido) {
                        const contenedorErrores = document.getElementById("contenedorErroresUnificado");
                        if (contenedorErrores) {
                            contenedorErrores.scrollIntoView({ behavior: 'smooth', block: 'center' });
                        }
                        return;
                    }

                    ocultarAlertErrores();

                    if (submitter?.dataset.operationMode === 'Create' && forceNumeroReassignmentInput?.value !== 'true') {
                        try {
                            const validationPayload = await validarNumeroCreate();
                            if (validationPayload?.requiresNumeroConfirmation) {
                                const requestedNumero = validationPayload.requestedNumero ?? (numeroInput?.value?.trim() || '');
                                const continuar = confirm(`El numero de hoja ${requestedNumero} ya fue utilizado por otro usuario. La presente hoja se guardara con el proximo numero disponible. Desea continuar?`);
                                if (!continuar) {
                                    return;
                                }

                                if (forceNumeroReassignmentInput) {
                                    forceNumeroReassignmentInput.value = 'true';
                                }

                                if (reservedNumeroInput) {
                                    reservedNumeroInput.value = '';
                                }
                            }
                        } catch (validationError) {
                            mostrarErrorEnAlert('No pudimos validar la numeracion de la hoja en este momento.');
                            return;
                        }
                    }

                    // Validacion basica de archivo antes de enviar
                    if (false && (!archivoRutaDoc.files || archivoRutaDoc.files.length === 0)) {
                        const adjuntosVal = document.getElementById('adjuntos')?.value;
                        if (!adjuntosVal || adjuntosVal.trim() === '') {
                            mostrarErrorEnAlert("Debe seleccionar el archivo el documento.");
                            return;
                        }
                    }

                    const operationResult = await ejecutarOperacionUpsert(submitter);

                    if (submitter?.dataset.operationMode === 'Create' && forceNumeroReassignmentInput) {
                        forceNumeroReassignmentInput.value = 'false';
                    }

                    if (archivoRutaDoc && operationResult?.success && submitter?.dataset.operationMode && submitter.dataset.operationMode !== 'Create') {
                        archivoRutaDoc.dataset.fileDirty = 'false';
                    }
                });
            }
        }
        function inicializarEventosArchivo() {
            const archivoRutaDoc = document.getElementById('archivoRutaDoc');
            const adjuntos = document.getElementById('adjuntos');
            const contenedorAdjunto = document.getElementById('contenedorAdjunto');
            const nombreAdjunto = document.getElementById('nombreAdjunto');

            if (adjuntos && contenedorAdjunto && nombreAdjunto) {
                // Si el modelo ya tiene un archivo adjunto (modo edición/visualización), mostrarlo
                if (adjuntos.value && adjuntos.value.trim() !== '') {
                    nombreAdjunto.textContent = adjuntos.value;
                    contenedorAdjunto.classList.remove('d-none');
                }
            }

            if (archivoRutaDoc && adjuntos) {
                archivoRutaDoc.dataset.fileDirty = 'false';

                archivoRutaDoc.addEventListener('change', function () {
                    if (this.files && this.files.length > 0) {
                        const fileName = this.files[0].name;
                        adjuntos.value = fileName;
                        this.dataset.fileDirty = 'true';
                        if (nombreAdjunto) nombreAdjunto.textContent = fileName;
                        if (contenedorAdjunto) contenedorAdjunto.classList.remove('d-none');
                    }

                    if (typeof window.actualizarValidaciones === 'function') {
                        window.actualizarValidaciones();
                    }
                });
            }
        }

        function eliminarAdjunto() {
            const confirmar = confirm("¿Desea quitar el archivo adjunto de la hoja?");
            if (!confirmar) {
                return;
            }

            const archivoRutaDoc = document.getElementById('archivoRutaDoc');
            const adjuntos = document.getElementById('adjuntos');
            const contenedorAdjunto = document.getElementById('contenedorAdjunto');

            if (archivoRutaDoc) archivoRutaDoc.value = ""; // Limpia el input file
            if (archivoRutaDoc) archivoRutaDoc.dataset.fileDirty = "false";
            if (adjuntos) adjuntos.value = "";
            if (contenedorAdjunto) contenedorAdjunto.classList.add('d-none');

            if (typeof window.actualizarValidaciones === 'function') {
                window.actualizarValidaciones();
            }
        }

        function mostrarErrorEnAlert(mensaje, errores = []) {
            const alertDiv = document.getElementById('contenedorErroresUnificado');
            const listaErrores = document.getElementById('listaErrores');
            
            if (!alertDiv || !listaErrores) return;

            // Limpiar lista previa
            listaErrores.innerHTML = '';
            
            // Si hay un mensaje principal, lo agregamos como un strong o primer item
            const mainMsg = document.createElement('li');
            const mainMessageText = document.createElement('strong');
            mainMessageText.textContent = mensaje;
            mainMessageText.style.whiteSpace = 'pre-line';
            mainMsg.appendChild(mainMessageText);
            listaErrores.appendChild(mainMsg);

            // Agregar detalles si existen
            errores.forEach(err => {
                const li = document.createElement('li');
                li.textContent = err;
                listaErrores.appendChild(li);
            });

            alertDiv.classList.remove('d-none');
            alertDiv.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }

        function obtenerMensajeTamanoArchivoExcedido() {
            const maxLabel = window.hojaUploadSettings?.maxFileSizeLabel || "60 MB";
            return `El archivo adjunto supera el máximo permitido de ${maxLabel}.`;
        }

        function ocultarAlertErrores() {
            const alertDiv = document.getElementById('contenedorErroresUnificado');
            if (alertDiv) alertDiv.classList.add('d-none');
        }

        function inicializarModalConfirmacion() {
            const modalElement = document.getElementById("confirmModal");
            const confirmText = document.getElementById("confirmText");
            const accionInput = document.getElementById("accion");
            const motivoContainer = document.getElementById("motivoContainer");
            const confirmForm = document.getElementById("confirmForm");
            const btnConfirmar = document.getElementById("btnConfirmar");
            const btnCancelar = document.getElementById("btnCancelar");

            if (!modalElement || !accionInput || !confirmText || !motivoContainer || !confirmForm) {
                return;
            }

            const modal = new bootstrap.Modal(modalElement);
            let currentAction = '';

            function configurarModal(texto, accion, mostrarMotivo) {
                confirmText.textContent = texto;
                accionInput.value = accion;
                currentAction = accion;
                motivoContainer.style.display = mostrarMotivo ? "block" : "none";

                modal.show();
            }

            confirmForm.addEventListener('submit', async function (e) {
                e.preventDefault();

                ocultarAlertErrores();
                const archivoInput = document.getElementById('archivoRutaDoc');
                
                if (archivoInput && currentAction === 'FIRMAR') {
                    const adjuntosVal = document.getElementById('adjuntos')?.value;
                    if ((!archivoInput.files || archivoInput.files.length === 0) && (!adjuntosVal || adjuntosVal.trim() === '')) {
                        mostrarErrorEnAlert("Por favor, adjunte el documento físico antes de Firmar.");
                        return;
                    }
                }

                await window.hojaUpsertProgress.runFormOperation({
                    form: confirmForm,
                    disableElements: [btnConfirmar, btnCancelar],
                    initialTitle: currentAction === 'FIRMAR' ? 'Firmando documento' : currentAction === 'RECHAZAR' ? 'Rechazando hoja' : 'Aprobando hoja',
                    initialMessage: currentAction === 'FIRMAR'
                        ? 'Estamos procesando la firma final del documento...'
                        : 'Estamos procesando la acción seleccionada...',
                    prepareFormData: function (formData) {
                        formData.set('accion', currentAction);

                        if (archivoInput && currentAction === 'FIRMAR' && archivoInput.files && archivoInput.files.length > 0) {
                            formData.set('archivoDoc', archivoInput.files[0]);
                        }

                        return true;
                    },
                    beforeOpenOverlay: function () {
                        modal.hide();
                    },
                    onExecutionErrorClosed: function (payload) {
                        mostrarErrorEnAlert(payload.message, payload.errors || []);
                    }
                });
            });

            modalElement.addEventListener('hidden.bs.modal', function () {
                const motivoRechazo = document.getElementById("motivoRechazo");
                if (motivoRechazo) motivoRechazo.value = "";

                currentAction = '';
            });

            const btnAprobar = document.getElementById("btnAprobar");
            if (btnAprobar) {
                btnAprobar.addEventListener("click", e => {
                    e.preventDefault();
                    configurarModal("¿Desea aprobar la hoja?", "APROBAR", false);
                });
            }

            const btnRechazar = document.getElementById("btnRechazar");
            if (btnRechazar) {
                btnRechazar.addEventListener("click", e => {
                    e.preventDefault();
                    configurarModal("¿Desea rechazar la hoja?", "RECHAZAR", true);
                });
            }

            const btnFirmar = document.getElementById("btnFirmar");
            if (btnFirmar) {
                btnFirmar.addEventListener("click", e => {
                    e.preventDefault();
                    configurarModal("¿Desea firmar la hoja?", "FIRMAR", false);
                });
            }
        }

        function validarGestorFinal() {

            const btnCrear = document.getElementById("btnCrear");
            const gestorFinal = document.getElementById("gestorFinal");

            if (!btnCrear || !gestorFinal) return;

            btnCrear.addEventListener("click", function (e) {

                if (gestorFinal.value && gestorFinal.value.trim() !== "") {
                    return;
                }

                const continuar = confirm(
                    "No seleccionó Gestor Final. ¿Desea continuar?"
                );

                if (!continuar) {
                    e.preventDefault(); // cancela el submit
                }
            });
        }

        function inicializarRevisores() {
            const revisoresFull = config.revisoresData;
            const registroGuardado = esRegistroGuardado();

            if (!registroGuardado) {
                inicializarRevisoresCreate(revisoresFull);
                inicializarSindico();
                return;
            }

            const selects = [
                { id: "reviso", next: "revisoGerente", fecha: "revisoFecha" },
                { id: "revisoGerente", next: "revisoEngagement", fecha: "revisoGerenteFecha" },
                { id: "revisoEngagement", next:"revisoSocioFirmante", fecha: "revisoEngagementFecha" },
                { id: "revisoSocioFirmante", next: null, fecha: "revisoSocioFirmanteFecha" }
            ];

            const preparoInput = document.getElementById("Preparo");
            const preparoValue = preparoInput?.dataset.value || "";
            const preparoCargo = getReviewerCargo(preparoValue, revisoresFull);

            inicializarCadenaAprobadores(selects, preparoCargo, revisoresFull);
            const valorReviso = getExistingSelectValue(document.getElementById("reviso"));

            const revisoSelect = document.getElementById("reviso");
            if (valorReviso) {
                asegurarOpcionExistenteEnSelect(revisoSelect, valorReviso, revisoresFull);
                revisoSelect.value = valorReviso;

               //Solo bloquear si el registro ya está guardado
                if (registroGuardado) {
                    revisoSelect.classList.add("readonly");
                    revisoSelect.disabled = true;
                }
                else {
                    revisoSelect.classList.remove("readonly");
                    revisoSelect.disabled = false;
                }
                // revisoSelect.classList.add("readonly");
                // revisoSelect.disabled = true;
            }

            selects.forEach((sel, index) => {
                if (sel.id === "reviso") return; // ya inicializamos reviso

                const selectElem = document.getElementById(sel.id);
                const valor = (selectElem.dataset.value || "").trim();
                const anterior = index > 0 ? document.getElementById(selects[index - 1].id) : null;

                if (valor) {
                    asegurarOpcionExistenteEnSelect(selectElem, valor, revisoresFull);
                    selectElem.value = valor;

                    if (registroGuardado) {
                        selectElem.classList.add("readonly");
                        selectElem.disabled = true;
                    } else {
                        selectElem.disabled = false;
                    }
                } else {
                    if (!registroGuardado) {
                        selectElem.disabled = false;
                    } else {
                        selectElem.disabled = anterior && !anterior.value;
                    }
                }
            });

            normalizarFlujoOpcional(selects, preparoCargo, revisoresFull, registroGuardado);
            marcarSiguientePendiente(selects);

            selects.forEach((sel, index) => {
                const current = document.getElementById(sel.id);
                const nextElem = sel.next ? document.getElementById(sel.next) : null;

                current.addEventListener("change", function () {
                    manejarCambioSelect(current, sel, index, selects, nextElem, revisoresFull);
                });
            });

            inicializarSindico();
        }

        function inicializarRevisoresCreate(revisoresFull) {
            const selects = [
                { id: "reviso", fecha: "revisoFecha" },
                { id: "revisoGerente", fecha: "revisoGerenteFecha" },
                { id: "revisoEngagement", fecha: "revisoEngagementFecha" }
            ];

            recalcularSelectoresCreate(selects, revisoresFull);

            selects.forEach((sel, index) => {
                const selectElem = document.getElementById(sel.id);
                if (!selectElem) return;

                selectElem.addEventListener("focus", function () {
                    selectElem.dataset.previousValue = selectElem.value || "";
                });

                selectElem.addEventListener("change", async function () {
                    await manejarCambioSelectCreate(selectElem, sel, index, selects, revisoresFull);
                });
            });

            marcarSiguientePendiente([...selects, { id: "revisoSocioFirmante" }]);
        }

        function recalcularSelectoresCreate(selects, revisoresFull) {
            selects.forEach((sel, index) => {
                const selectElem = document.getElementById(sel.id);
                if (!selectElem) return;

                const valorActual = selectElem.value || "";
                const nivelBase = obtenerNivelBaseCreate(index, selects, revisoresFull);
                llenarSelect(sel.id, nivelBase, valorActual, revisoresFull);
                selectElem.disabled = false;
                selectElem.classList.remove("readonly");
            });
        }

        function obtenerNivelBaseCreate(index, selects, revisoresFull) {
            for (let i = index - 1; i >= 0; i--) {
                const valorAnterior = document.getElementById(selects[i].id)?.value || "";
                const nivelAnterior = getReviewerCargo(valorAnterior, revisoresFull);
                if (nivelAnterior > 0) return nivelAnterior;
            }

            const preparoValue = document.getElementById("Preparo")?.dataset.value || "";
            return getReviewerCargo(preparoValue, revisoresFull);
        }

        async function manejarCambioSelectCreate(current, sel, index, selects, revisoresFull) {
            const valorAnterior = current.dataset.previousValue || "";
            const nivelActual = getReviewerCargo(current.value, revisoresFull);
            const tienePosteriorIncompatible = nivelActual > 0 && selects
                .slice(index + 1)
                .some(posterior => {
                    const nivelPosterior = getReviewerCargo(document.getElementById(posterior.id)?.value || "", revisoresFull);
                    return nivelPosterior > 0 && nivelPosterior <= nivelActual;
                });

            if (tienePosteriorIncompatible) {
                const revisorSeleccionado = findReviewer(revisoresFull, current.value);
                const nombreRevisorSeleccionado = revisorSeleccionado?.Detalle || current.selectedOptions[0]?.text || current.value;
                const continuar = await confirmarCambioJerarquiaRevisores(nombreRevisorSeleccionado);
                if (!continuar) {
                    current.value = valorAnterior;
                    current.dataset.value = valorAnterior;
                    current.dataset.previousValue = valorAnterior;
                    return;
                }

                limpiarRevisoresPosterioresCreate(index, selects);
            }

            current.dataset.value = current.value || "";
            current.dataset.previousValue = current.value || "";
            const fechaInput = document.getElementById(sel.fecha);
            if (fechaInput) {
                if (current.value) setFechaActual(sel.fecha);
                else fechaInput.value = "";
            }

            recalcularSelectoresCreate(selects, revisoresFull);
            marcarSiguientePendiente([...selects, { id: "revisoSocioFirmante" }]);
        }

        function limpiarRevisoresPosterioresCreate(index, selects) {
            selects.slice(index + 1).forEach(sel => {
                const selectElem = document.getElementById(sel.id);
                if (!selectElem) return;

                selectElem.value = "";
                selectElem.dataset.value = "";
                selectElem.dataset.previousValue = "";

                const fechaInput = document.getElementById(sel.fecha);
                if (fechaInput) fechaInput.value = "";
            });
        }

        function confirmarCambioJerarquiaRevisores(nombreRevisorSeleccionado) {
            return new Promise(resolve => {
                const modalElement = document.getElementById("confirmacionJerarquiaRevisoresModal");
                const mensaje = document.getElementById("confirmacionJerarquiaRevisoresMensaje");
                const continuar = document.getElementById("continuarCambioJerarquiaRevisores");
                const cancelar = document.getElementById("cancelarCambioJerarquiaRevisores");

                if (!modalElement || !mensaje || !continuar || !cancelar || !window.bootstrap) {
                    resolve(window.confirm(`El usuario ${nombreRevisorSeleccionado} tiene un nivel superior al siguiente revisor del flujo. Si continuás, se conservará tu selección y deberás volver a seleccionar los revisores siguientes.`));
                    return;
                }

                mensaje.textContent = `El usuario ${nombreRevisorSeleccionado} tiene un nivel superior al siguiente revisor del flujo. Si continuás, se conservará tu selección y deberás volver a seleccionar los revisores siguientes.`;
                const modal = bootstrap.Modal.getOrCreateInstance(modalElement, { backdrop: "static", keyboard: false });
                let resuelto = false;

                const finalizar = resultado => {
                    if (resuelto) return;
                    resuelto = true;
                    continuar.removeEventListener("click", confirmar);
                    cancelar.removeEventListener("click", rechazar);
                    modalElement.removeEventListener("hidden.bs.modal", cerrarSinConfirmar);
                    modal.hide();
                    resolve(resultado);
                };
                const confirmar = () => finalizar(true);
                const rechazar = () => finalizar(false);
                const cerrarSinConfirmar = () => finalizar(false);

                continuar.addEventListener("click", confirmar);
                cancelar.addEventListener("click", rechazar);
                modalElement.addEventListener("hidden.bs.modal", cerrarSinConfirmar);
                modal.show();
            });
        }

        function inicializarCadenaAprobadores(selects, preparoCargo, revisoresFull) {
            let cargoBaseActual = preparoCargo;

            selects.forEach(sel => {
                if (sel.id === "revisoSocioFirmante") {
                    return;
                }

                const selectElem = document.getElementById(sel.id);
                if (!selectElem) {
                    return;
                }

                const valorExistente = getExistingSelectValue(selectElem);
                llenarSelect(sel.id, cargoBaseActual, valorExistente, revisoresFull);

                const cargoSeleccionado = getReviewerCargo(valorExistente, revisoresFull);
                if (cargoSeleccionado > 0) {
                    cargoBaseActual = cargoSeleccionado;
                }
            });
        }

        function normalizarFlujoOpcional(selects, preparoCargo, revisoresFull, registroGuardado) {
            let cargoBaseActual = preparoCargo;

            selects.forEach(sel => {
                const selectElem = document.getElementById(sel.id);
                if (!selectElem) {
                    return;
                }

                if (sel.id !== "revisoSocioFirmante") {
                    const valorExistente = getExistingSelectValue(selectElem);
                    llenarSelect(sel.id, cargoBaseActual, valorExistente, revisoresFull);

                    const cargoSeleccionado = getReviewerCargo(valorExistente, revisoresFull);
                    if (cargoSeleccionado > 0) {
                        cargoBaseActual = cargoSeleccionado;
                    }
                }

                const valorActual = getExistingSelectValue(selectElem);
                if (valorActual) {
                    asegurarOpcionExistenteEnSelect(selectElem, valorActual, revisoresFull);
                    selectElem.value = valorActual;

                    if (registroGuardado && sel.id !== "revisoSocioFirmante") {
                        selectElem.classList.add("readonly");
                        selectElem.disabled = true;
                    } else {
                        selectElem.classList.remove("readonly");
                        selectElem.disabled = false;
                    }
                } else {
                    selectElem.classList.remove("readonly");
                    selectElem.disabled = false;
                }
            });
        }

        function manejarCambioSelect(current, sel, index, selects, nextElem, revisoresFull) {
            if (!current.value) {
                current.classList.remove("readonly");
                const fechaInput = document.getElementById(sel.fecha);
                if (fechaInput) fechaInput.value = "";
                limpiarSelectsDesde(index + 1, selects);
                marcarSiguientePendiente(selects);
                return;
            }

            const selectedOpt = current.selectedOptions[0];
            const cargoActual = selectedOpt ? parseInt(selectedOpt.dataset.cargo || 0, 10) : null;

            if (cargoActual !== null) {
                for (let i = index + 1; i < selects.length; i++) {
                    const inferior = document.getElementById(selects[i].id);
                    if (!inferior) continue;

                    if (inferior.id === "revisoSocioFirmante") continue;

                    const valorInf = (inferior.dataset.value || inferior.value || "").trim();
                    if (!valorInf) continue;

                    const revInf = findReviewer(revisoresFull, valorInf);
                    if (!revInf) continue;

                    const cargoInf = parseInt(revInf.Cargo || "0", 10);
                    if (isNaN(cargoInf)) continue;

                    if (cargoInf <= cargoActual) {
                        inferior.value = "";
                        inferior.dataset.value = "";
                        const fechaInf = document.getElementById(selects[i].fecha);
                        if (fechaInf) fechaInf.value = "";
                        const placeholder = inferior.getAttribute('data-placeholder') || '-- Seleccione --';
                        inferior.innerHTML = `<option value="">${placeholder}</option>`;
                        inferior.disabled = false; // Aseguramos que quede habilitado
                    }
                }
            }

            // if (cargoActual > 0 && nextElem) {
            //     const valorExistenteSiguiente = getExistingSelectValue(nextElem);
            //     llenarSelect(sel.next, cargoActual, valorExistenteSiguiente, revisoresFull);

            if (cargoActual > 0 && nextElem) {
                const valorExistenteSiguiente = getExistingSelectValue(nextElem);

                if (nextElem.id != "revisoSocioFirmante")
                {
                    llenarSelect(sel.next, cargoActual, valorExistenteSiguiente, revisoresFull);
                }
            }

            setFechaActual(sel.fecha);
            current.classList.remove("readonly");
            marcarSiguientePendiente(selects);
        }


        function marcarSiguientePendiente(selects) {
            selects.forEach(s => document.getElementById(s.id).classList.remove("is-valid"));

            const pendiente = selects.find(s => {
                const el = document.getElementById(s.id);
                return !el.classList.contains("readonly") && !el.disabled && !el.value;
            });

            if (pendiente) {
                const elem = document.getElementById(pendiente.id);
                elem.classList.add("is-valid");
                try { elem.focus(); } catch (e) {}
            }
        }

        function asegurarOpcionExistenteEnSelect(selectElem, valorExistente, revisoresFull) {
            if (!valorExistente) return;
            if ([...selectElem.options].some(o => o.value === valorExistente)) return;

            const revisor = findReviewer(revisoresFull, valorExistente);
            if (!revisor) return;

            const opt = document.createElement("option");
            opt.value = revisor.Empleado;
            opt.textContent = `${revisor.Detalle} (${revisor.Area || ''})`;
            opt.dataset.cargo = revisor.Cargo;
            selectElem.appendChild(opt);
        }

        function llenarSelect(selectId, cargoBase, valorExistente, revisoresFull) {
            const selectElem = document.getElementById(selectId);
            if (!selectElem) return;

            const seleccionActual = valorExistente || selectElem.value || null;
            const baseLevel = Number.isFinite(cargoBase) ? cargoBase : 0;
            const filtrados = revisoresFull.filter(r => {
                const reviewerLevel = parseInt(r.Cargo || "0", 10);
                return !isNaN(reviewerLevel) && reviewerLevel > baseLevel;
            });

            const placeholder = selectElem.getAttribute('data-placeholder') || '-- Seleccione --';
            selectElem.innerHTML = `<option value="">${placeholder}</option>`;

            if (seleccionActual) {
                const existe = findReviewer(revisoresFull, seleccionActual);
                if (existe && !filtrados.some(f => f.Empleado === seleccionActual)) {
                    const optExist = document.createElement("option");
                    optExist.value = existe.Empleado;
                    //optExist.textContent = `${existe.Detalle} (${existe.Area || ''}) (Retenido)`;
                    optExist.textContent = `${existe.Detalle}`;
                    optExist.dataset.cargo = existe.Cargo;
                    selectElem.appendChild(optExist);
                }
            }

            filtrados.forEach(r => {
                const opt = document.createElement("option");
                opt.value = r.Empleado;
                opt.textContent = `${r.Detalle} (${r.Area || ''})`;
                opt.dataset.cargo = r.Cargo;
                selectElem.appendChild(opt);
            });

            if (seleccionActual) {
                selectElem.value = seleccionActual;
                selectElem.dataset.value = seleccionActual; // sincroniza con dataset
            }

            selectElem.disabled = false;
        }

        function limpiarSelectsDesde(startIndex, selects) {
            for (let i = startIndex; i < selects.length; i++) {
                const s = document.getElementById(selects[i].id);

                if (s && s.id === "revisoSocioFirmante") {
                    continue;
                }

                const placeholder = s.getAttribute('data-placeholder') || '-- Seleccione --';
                s.innerHTML = `<option value="">${placeholder}</option>`;
                s.value = "";
                s.disabled = true;
                s.classList.remove("select-pendiente", "readonly");

                const fechaInput = document.getElementById(selects[i].fecha);
                if (fechaInput) fechaInput.value = "";
            }
        }

        function esRegistroGuardado() {
            if (!idHoja) return false;

            const idValue = idHoja.value?.trim();
            return idValue !== "" && idValue !== null && idValue !== undefined;
        }

        function onSocioFirmanteFocus(selectElement)
        {
            oldSocioFirmante = selectElement.value;
        }

        function onSocioFirmanteChange(selectElement) {
            debugger;
            const fechaInput = document.getElementById("revisoSocioFirmanteFecha");
            if (selectElement.value) {
                setFechaActual("revisoSocioFirmanteFecha");
            } else {
                fechaInput.value = "";
            }

            const oldValue = oldSocioFirmante;
            const manejador = document.getElementById("Manejador");

            if (oldValue == manejador.value && oldValue != selectElement.value)
            {
                manejador.value = selectElement.value;
            }
        }

        function obtenerRevisoresSeleccionados(selects) {
            return selects
                .filter(s => s.id !== "revisoSocioFirmante")
                .map(s => document.getElementById(s.id)?.value)
                .filter(v => v);
        }

        function getExistingSelectValue(selectElem) {
            if (!selectElem) return null;
            const ds = (selectElem.dataset && selectElem.dataset.value) ? selectElem.dataset.value.trim() : "";
            const val = (selectElem.value) ? selectElem.value.trim() : "";
            return ds || val || null;
        }

        function normalizeReviewerIdentifier(value) {
            return (value || "").toString().trim().toUpperCase();
        }

        function findReviewer(revisoresFull, identifier) {
            const normalizedIdentifier = normalizeReviewerIdentifier(identifier);
            if (!normalizedIdentifier) {
                return null;
            }

            return revisoresFull.find(r =>
                normalizeReviewerIdentifier(r.Empleado) === normalizedIdentifier ||
                normalizeReviewerIdentifier(r.Mail) === normalizedIdentifier) || null;
        }

        function getReviewerCargo(identifier, revisoresFull) {
            const reviewer = findReviewer(revisoresFull, identifier);
            const reviewerLevel = parseInt(reviewer?.Cargo || "0", 10);
            return Number.isFinite(reviewerLevel) ? reviewerLevel : 0;
        }


        function setFechaActual(inputId) {
            const input = document.getElementById(inputId);
            if (!input) return;
            const hoy = new Date();
            const dia = String(hoy.getDate()).padStart(2, '0');
            const mes = String(hoy.getMonth() + 1).padStart(2, '0');
            const anio = hoy.getFullYear();
            input.value = `${dia}/${mes}/${anio}`;
        }

        function validateModel() {

            const form = document.querySelector("form");
            if (!form) return;

            form.querySelectorAll("input, select, textarea").forEach(input => {
                input.addEventListener("change", function () {
                    const name = this.getAttribute("name");
                    const validationSpan = form.querySelector(`span[data-valmsg-for='${name}']`);

                    if (this.value && this.value.trim() !== "") {
                        this.classList.remove("is-invalid");
                        this.classList.add("is-valid");
                        if (validationSpan) validationSpan.textContent = "";

                        setTimeout(() => {
                            this.classList.remove("is-valid");
                        }, 2000);

                    } else {
                        this.classList.remove("is-valid");
                        this.classList.add("is-invalid");

                        setTimeout(() => {
                            this.classList.remove("is-invalid");
                        }, 2000);
                    }
                });
            });
        }

        var subareas = JSON.parse(config.subareasJson || '[]');
        var clientesData = JSON.parse(config.clientesJson || '[]');

        //AVISAR SI HAY CAMBIOS PARA GUARDAR AL ABANDONAR LA HOJA
        // (function () {
        //     let form = document.getElementById("hojaForm");
        //     let isDirty = false;
        //     debugger;

        //     // Detectar cambios en cualquier campo del formulario
        //     form.querySelectorAll("input, select, textarea").forEach(campo => {
        //         campo.addEventListener("change", () => {
        //             isDirty = true;
        //         });
        //         campo.addEventListener("input", () => {
        //             isDirty = true;
        //         });
        //     });

        //     // Desactivar advertencia al enviar el formulario
        //     form.addEventListener("submit", () => {
        //         isDirty = false;
        //     });

        //     // Detectar intento de cerrar o refrescar página
        //     window.addEventListener("beforeunload", function (e) {
        //         debugger;
        //         if (isDirty) {
        //             e.preventDefault();
        //             e.returnValue = "Hay cambios sin guardar. ¿Seguro que quieres salir?";
        //         }
        //     });
        // })();

        function onSectorChange(select) {
                var sector = select.value;
                cargarSubareas(sector);
        }

        function cargarSubareas(sector, selectedSubareaName = null) {
            var subareaSelect = document.getElementById("subarea");

            subareaSelect.innerHTML = '<option value="">Seleccione SubArea</option>';
            subareaSelect.disabled = false;
            subareaSelect.required = true;

            if (sector) {
                var filtradas = subareas.filter(s => s.Sector === sector);

                if (filtradas.length === 0) {
                    subareaSelect.innerHTML = '<option value="No aplica" selected>No aplica</option>';
                    return;
                }

                filtradas.forEach(function (s) {
                    var option = document.createElement("option");
                    option.value = s.Nombre;
                    option.text = s.Nombre;

                    if (selectedSubareaName && selectedSubareaName === s.Nombre) {
                        option.selected = true;
                    }

                    subareaSelect.appendChild(option);
                });
            }
        }

        function inicializarSindico() {
            const isSindicoSelect = document.getElementById("IsSindico");
            const sindicoSelect = document.getElementById("Sindico");

            if (!isSindicoSelect || !sindicoSelect) return;

            const valorSindico = (sindicoSelect.dataset.value || sindicoSelect.value || "").trim();

            if (valorSindico) {
                isSindicoSelect.disabled = true;
                sindicoSelect.disabled = true;
                return;
            }

            isSindicoSelect.addEventListener("change", function () {
                toggleSindico(this.value);
            });

            toggleSindico(isSindicoSelect.value);
        }

        function toggleSindico(selectElement) {
            var isSindicoValue = selectElement;
            var sindicoSelect = document.getElementById('Sindico');

            if (isSindicoValue === 'true') {
                sindicoSelect.disabled = null;
            }
            else {
                sindicoSelect.disabled = true;
                sindicoSelect.value = "";
            }
        }

        function addClassToLabels(className) {
            var labels = document.querySelectorAll('label');

            if (labels.length === 0) {
                return;
            }

            labels.forEach(function(label) {
                label.classList.add(className);
            });
        }

        function inicializarNombreGenerico() {
            const nombreGenericoSelect = document.getElementById("NombreGenerico");
            const contratosSelect = document.getElementById("ContratoPlataforma");
            const fechaCierreDiv = document.getElementById("fechaDeCierreDiv");
            const fechaCierre = document.getElementById("fechaDeCierre");
            const btnAuditoria = document.getElementById("btnAuditoria");
            const genericosData = config.genericosData;

            if (!nombreGenericoSelect || !contratosSelect) return;

            function requiereAuditoria(valorSeleccionado) {
                return (valorSeleccionado || "").trim().toLowerCase() === auditoriaNombreObjetivo.toLowerCase();
            }

            function procesarNombreGenerico(valorSeleccionado, esCargaInicial = false) {
                const generico = genericosData.find(x => x.NombreGenerico === valorSeleccionado);
                const contratoActual = (contratosSelect.value || "").trim();
                const tieneContratoReal = contratoActual !== "" && contratoActual !== "Sin Contrato";

                const sinContratoExistente = Array.from(contratosSelect.options)
                    .find(opt => opt.value === "Sin Contrato");

                if (sinContratoExistente) {
                    contratosSelect.removeChild(sinContratoExistente);
                }

                fechaCierreDiv.classList.add("invisible");
                btnAuditoria.classList.add("invisible");
                if (!esCargaInicial) fechaCierre.value = "";

                //Si es "Propuesta", agregar opción "Sin Contrato"
                if (generico && generico.Categoria === "Propuesta") {
                    const sinContratoOption = document.createElement("option");
                    sinContratoOption.value = "Sin Contrato";
                    sinContratoOption.text = "Sin Contrato";
                    contratosSelect.appendChild(sinContratoOption);
                    if (!tieneContratoReal) {
                        contratosSelect.value = "Sin Contrato";
                    }
                }

                //Si es "Informe del auditor", mostrar campo fecha de cierre y acceso al modal
                if (requiereAuditoria(valorSeleccionado)) {
                    fechaCierreDiv.classList.remove("invisible");
                    btnAuditoria.classList.remove("invisible");

                } else {
                    limpiarBorradorAuditoria();
                }
            }

            nombreGenericoSelect.addEventListener("change", function () {
                procesarNombreGenerico(this.value);
            });

            const valorInicial = nombreGenericoSelect.value;
            if (valorInicial) {
                procesarNombreGenerico(valorInicial, true);
            }
        }

        function actualizarCodigoPlataforma(clienteId) {
            var inputCodCliente = document.getElementById('CodCliente');
            var selectContratos = document.getElementById('ContratoPlataforma');

            if (!clienteId) {
                inputCodCliente.value = '';
                return;
            }

            var clienteSeleccionado = clientesData.find(c => c.Id == clienteId);

            if (clienteSeleccionado) {
                inputCodCliente.value = clienteSeleccionado.CodigoPlataforma;
                codClientePlataforma = clienteSeleccionado.CodigoPlataforma;

                const hayContratosPrecargados = selectContratos
                    && Array.from(selectContratos.options).some(option => option.value && option.value.trim() !== "");

                if (!isInitialLoad || !hayContratosPrecargados) {
                    inputCodCliente.dispatchEvent(new Event('change'));
                }
            } else {
                inputCodCliente.value = '';
            }

            //Limpiar nombre generico al cambiar cliente
            if (!isInitialLoad) {
                selectNombreGenerico.selectedIndex = 0;
            }
        }

        function cargarContratos(codCliente, selectedContract = null) {
            var selectContratos = document.getElementById('ContratoPlataforma');
            const existingOptions = Array.from(selectContratos.options)
                .map(option => option.value)
                .filter(value => value && value.trim() !== "");
            const currentValue = selectContratos.value;
            const effectiveSelectedContract = selectedContract || currentValue;

            if (existingOptions.length > 0 && effectiveSelectedContract) {
                if (existingOptions.includes(effectiveSelectedContract)) {
                    selectContratos.value = effectiveSelectedContract;
                }

                delete selectContratos.dataset.loading;
                selectContratos.dispatchEvent(new Event('change'));
                return;
            }

            selectContratos.innerHTML = '<option value="">Cargando contratos...</option>';
            selectContratos.dataset.loading = "true"; // marcar como en carga

            if (!codCliente) {
                selectContratos.innerHTML = '<option value="">Seleccione Contrato Plataforma</option>';
                delete selectContratos.dataset.loading;
                return;
            }

            fetch('/Home/GetContratosByCodigo?codigoPlataforma=' + encodeURIComponent(codCliente))
                .then(response => response.json())
                .then(data => {
                    selectContratos.innerHTML = '<option value="">Seleccione Contrato Plataforma</option>';

                    data.forEach(function(contrato) {
                        var option = document.createElement('option');
                        option.value = contrato.value;
                        option.text = contrato.text;

                        if (selectedContract && selectedContract === contrato.value) {
                            option.selected = true;
                        }

                        selectContratos.appendChild(option);
                    });
                    
                    // Quitar marca de carga y notificar para refrescar validaciones
                    delete selectContratos.dataset.loading;
                    selectContratos.dispatchEvent(new Event('change'));
                })
                .catch(error => {
                    console.error('Error al cargar contratos:', error);
                    selectContratos.innerHTML = '<option value="">Error al cargar los contratos</option>';
                    delete selectContratos.dataset.loading;
                });
        }

        function inicializarFormulario() {
            //retener valor de subarea
            var areaSelect = document.getElementById('Sector');

            if (areaSelect && areaSelect.value) {
                var currentArea = areaSelect.value;

                var currentSubarea = config.currentSubarea;

                cargarSubareas(currentArea, currentSubarea);
            }

            //retener valor de contratos
            var codCliente = config.codCliente || codClientePlataforma;
            var selectedContract = config.selectedContract;
            var selectContratos = document.getElementById('ContratoPlataforma');
            const hayContratosPrecargados = selectContratos
                && Array.from(selectContratos.options).some(option => option.value && option.value.trim() !== "");

            if (hayContratosPrecargados && selectedContract) {
                const existingOption = Array.from(selectContratos.options)
                    .find(option => option.value === selectedContract);

                if (!existingOption) {
                    const option = document.createElement('option');
                    option.value = selectedContract;
                    option.text = selectedContract;
                    option.selected = true;
                    selectContratos.appendChild(option);
                }

                selectContratos.value = selectedContract;
                selectContratos.dispatchEvent(new Event('change'));
            }
            else if (codCliente) {
                cargarContratos(codCliente, selectedContract);
            }
        }

        const camposImportesAuditoria = ["Activo", "Pasivo", "PatrimonioNeto", "Resultado", "TotalIngresos", "TotalOtrosIngresos"];
        const camposAuditoria = [...camposImportesAuditoria, "Moneda", "TipoNumeracion"];

        function normalizarImporteAuditoria(valor) {
            const texto = (valor || "").trim();
            if (!texto) return { valido: true, valor: "" };

            const formatoArgentino = /^-?(?:\d+|\d{1,3}(?:\.\d{3})+)(?:,\d{1,2})?$/;
            if (!formatoArgentino.test(texto)) {
                return { valido: false, mensaje: "Ingrese un formato de número válido, por ejemplo 1.234,56." };
            }

            const negativo = texto.startsWith("-");
            const sinSigno = negativo ? texto.substring(1) : texto;
            const [parteEntera, parteDecimal = ""] = sinSigno.split(",");
            const entero = parteEntera.replaceAll(".", "").replace(/^0+(?=\d)/, "") || "0";
            const enteroFormateado = entero.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
            return { valido: true, valor: `${negativo ? "-" : ""}${enteroFormateado},${parteDecimal.padEnd(2, "0")}` };
        }

        function formatearImporteAuditoria(valor) {
            if (valor === null || valor === undefined || valor === "") return "";
            const numero = typeof valor === "number" ? valor : Number(valor);
            return Number.isFinite(numero)
                ? new Intl.NumberFormat("es-AR", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(numero)
                : "";
        }

        function validarYNormalizarAuditoria(form) {
            let esValido = true;
            camposImportesAuditoria.forEach(campo => {
                const input = form.querySelector(`#${campo}`);
                const span = form.querySelector(`span[data-valmsg-for='${campo}']`);
                if (!input?.value?.trim()) {
                    esValido = false;
                    if (span) span.textContent = "Debe completar este importe.";
                    return;
                }

                const resultado = normalizarImporteAuditoria(input?.value);
                if (!resultado.valido) {
                    esValido = false;
                    if (span) span.textContent = resultado.mensaje;
                    return;
                }

                if (input) input.value = resultado.valor;
                if (span) span.textContent = "";
            });

            ["Moneda", "TipoNumeracion"].forEach(campo => {
                const input = form.querySelector(`#${campo}`);
                const span = form.querySelector(`span[data-valmsg-for='${campo}']`);
                if (!input?.value?.trim()) {
                    esValido = false;
                    if (span) span.textContent = "Debe seleccionar un valor.";
                } else if (span) {
                    span.textContent = "";
                }
            });

            if (esValido) {
                const aCentavos = campo => Math.round(Number(
                    form.querySelector(`#${campo}`).value.replaceAll(".", "").replace(",", ".")) * 100);
                if (aCentavos("Activo") !== aCentavos("Pasivo") + aCentavos("PatrimonioNeto")) {
                    esValido = false;
                    const span = form.querySelector("span[data-valmsg-for='Activo']");
                    if (span) span.textContent = "El Activo debe ser igual a la suma de Pasivo + Patrimonio Neto.";
                }
            }
            return esValido;
        }

        function copiarBorradorAuditoriaAlFormularioHoja(form) {
            camposAuditoria.forEach(campo => {
                const origen = form.querySelector(`#${campo}`);
                const destino = document.getElementById(`Auditoria${campo}`);
                if (origen && destino) destino.value = origen.value;
            });
        }

        function cargarBorradorAuditoria(form) {
            camposAuditoria.forEach(campo => {
                const origen = document.getElementById(`Auditoria${campo}`);
                const destino = form.querySelector(`#${campo}`);
                if (origen && destino) destino.value = origen.value;
            });
        }

        function limpiarBorradorAuditoria() {
            camposAuditoria.forEach(campo => {
                const input = document.getElementById(`Auditoria${campo}`);
                if (input) input.value = "";
            });
        }

        function guardarAuditoria() {
            const form = document.getElementById("formAuditoria");
            const modalElement = document.getElementById("modalAuditoria");
            const modal = bootstrap.Modal.getInstance(modalElement);
            const btnGuardar = document.getElementById("btnGuardarAuditoria");

            form.querySelectorAll(".text-danger").forEach(span => span.textContent = "");

            if (!validarYNormalizarAuditoria(form)) {
                return;
            }

            const hojaId = document.getElementById("Id")?.value?.trim() || "";
            if (!hojaId) {
                copiarBorradorAuditoriaAlFormularioHoja(form);
                if (modal) modal.hide();
                return;
            }

            window.hojaUpsertProgress.runFormOperation({
                form: form,
                url: '/Home/SaveAuditoria',
                disableElements: [btnGuardar],
                initialTitle: 'Guardando auditoría',
                initialMessage: 'Estamos registrando la información de auditoría...',
                beforeOpenOverlay: function () {
                    if (modal) {
                        modal.hide();
                    }
                },
                onPreflightError: function (data) {
                    if (!data.validationErrors) {
                        return false;
                    }

                    data.validationErrors.forEach(err => {
                        const inputName = (err.campo || "")
                            .split('.')
                            .pop();

                        const span = form.querySelector(`span[data-valmsg-for='${inputName}']`);
                        if (span) {
                            span.textContent = err.errores.join(", ");
                        }
                    });

                    if (modal) {
                        modal.show();
                    }

                    return true;
                },
                onExecutionErrorClosed: function (payload) {
                    mostrarErrorEnAlert(payload.message, payload.errors || []);
                    if (modal) {
                        modal.show();
                    }
                }
            });
        }

        function abrirModalAuditoria() {
            const form = document.getElementById("formAuditoria");
            form.reset();
            form.querySelectorAll(".text-danger").forEach(span => span.textContent = "");
            const hojaId = document.getElementById("Id")?.value?.trim() || "";

            if (!hojaId) {
                cargarBorradorAuditoria(form);
                const modal = new bootstrap.Modal(document.getElementById("modalAuditoria"));
                modal.show();
                return;
            }

            // Asignar siempre el id de hoja
            document.getElementById("HojaId").value = hojaId;

            // Llamar a la API con fetch
            fetch('/Home/GetAuditoriaById?IdHoja=' + encodeURIComponent(hojaId))
                .then(response => {
                    if (!response.ok)
                    {
                        throw new Error("Error al obtener la auditoría");
                    }

                    return response.json();
                })
                .then(result => {
                    if (result.success === false) {
                        mostrarErrorEnAlert(result.message || "No pudimos cargar la auditoría en este momento.");
                        return;
                    }

                    if (result.exists) {
                        const data = result.data;
                        // Completar los campos con los valores existentes
                        document.getElementById("Activo").value = formatearImporteAuditoria(data.activo);
                        document.getElementById("Pasivo").value = formatearImporteAuditoria(data.pasivo);
                        document.getElementById("PatrimonioNeto").value = formatearImporteAuditoria(data.patrimonioNeto);
                        document.getElementById("Moneda").value = data.moneda ?? "";
                        document.getElementById("TipoNumeracion").value = data.tipoNumeracion ?? "";
                        document.getElementById("Resultado").value = formatearImporteAuditoria(data.resultado);
                        document.getElementById("TotalIngresos").value = formatearImporteAuditoria(data.totalIngresos);
                        document.getElementById("TotalOtrosIngresos").value = formatearImporteAuditoria(data.totalOtrosIngresos);
                    }

                    // Abre el modal
                    const modal = new bootstrap.Modal(document.getElementById("modalAuditoria"));
                    modal.show();
                })
                .catch(err => {
                    console.error(err);
                    mostrarErrorEnAlert("No pudimos cargar la auditoría en este momento. Intentá nuevamente en unos instantes.");
                });
        }

        function inicializarSpinnerDescarga() {
            debugger;
            const form = document.getElementById("formDescargarHoja");
            if (!form) return;

            form.addEventListener("submit", function (e) {
                const submitBtn = form.querySelector("button[type='submit']");
                if (!submitBtn) return;

                submitBtn.disabled = true;

                const originalHtml = submitBtn.innerHTML;

                submitBtn.innerHTML = `
                    <span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span>
                    Generando...
                `;

                const cookieName = "archivoDescargado";

                document.cookie = cookieName + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/";

                const checkInterval = setInterval(() => {
                    if (document.cookie.includes(cookieName + "=1")) {
                        clearInterval(checkInterval);

                        submitBtn.disabled = false;
                        submitBtn.innerHTML = originalHtml;

                        document.cookie = cookieName + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/";
                    }
                }, 1000);
            });
        }

    

        document.addEventListener("DOMContentLoaded", function() {
            // Campos que tienen el asterisco (*) en el HTML
            const fieldsToValidate = [
                { id: "Cliente", name: "Cliente" },
                { id: "Sector", name: "Sector" },
                { id: "subarea", name: "Subárea" },
                { id: "NombreGenerico", name: "Tipo de Documento" },
                { id: "descripcion", name: "Descripción" },
                { id: "ContratoPlataforma", name: "Contrato Plataforma" },
                { id: "revisoSocioFirmante", name: "Socio Firmante" },
                { id: "lugarFirmaDoc", name: "Lugar de firma" },
                { id: "rutaPapeles", name: "Ruta papeles" },
                { id: "rutaDoc", name: "Ruta doc." }
            ];

            let formularioIntentado = false;

            function validarAdjuntoDocumento() {
                const archivoInput = document.getElementById("archivoRutaDoc");
                const adjuntosInput = document.getElementById("adjuntos");
                const tieneArchivoSeleccionado = !!(archivoInput && archivoInput.files && archivoInput.files.length > 0);
                const tieneAdjuntoGuardado = !!(adjuntosInput && adjuntosInput.value && adjuntosInput.value.trim() !== "");
                const esValido = tieneArchivoSeleccionado || tieneAdjuntoGuardado;

                return {
                    esValido,
                    mensaje: esValido ? "" : "Falta adjuntar el documento fisico."
                };
            }

            window.validarAdjuntoDocumento = validarAdjuntoDocumento;

            function validarTamanoArchivoAdjunto() {
                const archivoInput = document.getElementById("archivoRutaDoc");
                const maxFileSizeBytes = window.hojaUploadSettings?.maxFileSizeBytes || 0;
                const archivo = archivoInput?.files && archivoInput.files.length > 0
                    ? archivoInput.files[0]
                    : null;

                if (!archivo || maxFileSizeBytes <= 0) {
                    return {
                        esValido: true,
                        mensaje: ""
                    };
                }

                if (archivo.size <= maxFileSizeBytes) {
                    return {
                        esValido: true,
                        mensaje: ""
                    };
                }

                return {
                    esValido: false,
                    mensaje: obtenerMensajeTamanoArchivoExcedido()
                };
            }

            window.validarTamanoArchivoAdjunto = validarTamanoArchivoAdjunto;

            // Función global de validación (accesible desde reverificarArchivo)
            window.actualizarValidaciones = function() {
                const contenedorErrores = document.getElementById("contenedorErroresUnificado");
                const listaErrores = document.getElementById("listaErrores");
                const btnFirmar = document.getElementById("btnFirmar");
                const btnCrear = document.getElementById("btnCrear");
                
                if (!contenedorErrores || !listaErrores) return true;

                // 1. Preservar el <li> de error de archivo y limpiar el resto
                const errorArchivoLi = document.getElementById("errorArchivoFisico");
                listaErrores.innerHTML = "";
                if (errorArchivoLi) listaErrores.appendChild(errorArchivoLi);

                // 2. Validar campos obligatorios SOLO si el usuario ya intentó enviar el formulario
                let hayErroresDeCampos = false;
                if (formularioIntentado) {
                    fieldsToValidate.forEach(field => {
                        const elem = document.getElementById(field.id);
                        if (!elem) return;
                        if (elem.dataset.loading === "true") return; // campo en carga: ignorar
                        if (!elem.value || elem.value.trim() === "") {
                            const li = document.createElement("li");
                            li.textContent = `Falta completar el campo obligatorio: ${field.name}`;
                            li.className = "error-campo-js";
                            listaErrores.appendChild(li);
                            hayErroresDeCampos = true;
                        }
                    });

                    const validacionAdjunto = validarAdjuntoDocumento();
                    if (!validacionAdjunto.esValido) {
                        const li = document.createElement("li");
                        li.textContent = validacionAdjunto.mensaje;
                        li.className = "error-campo-js";
                        listaErrores.appendChild(li);
                        hayErroresDeCampos = true;
                    }

                    const validacionTamanoArchivo = validarTamanoArchivoAdjunto();
                    if (!validacionTamanoArchivo.esValido) {
                        const li = document.createElement("li");
                        li.textContent = validacionTamanoArchivo.mensaje;
                        li.className = "error-campo-js";
                        listaErrores.appendChild(li);
                        hayErroresDeCampos = true;
                    }
                }

                // 3. Detectar error crítico de archivo
                const errorArchivoElem = document.getElementById("errorArchivoFisico");
                const hayErrorArchivoCritico = !!(errorArchivoElem && errorArchivoElem.dataset.severity === "error");

                // 4. Mostrar/Ocultar contenedor rojo
                const tieneMensajes = listaErrores.querySelectorAll("li").length > 0;
                if (tieneMensajes && (hayErrorArchivoCritico || (formularioIntentado && hayErroresDeCampos))) {
                    contenedorErrores.classList.remove("d-none");
                } else {
                    contenedorErrores.classList.add("d-none");
                }

                // 5. Botón Firmar: SOLO se bloquea si el archivo no existe físicamente
                if (btnCrear) {
                    btnCrear.disabled = formularioIntentado && hayErroresDeCampos;
                }

                if (btnFirmar) {
                    btnFirmar.disabled = hayErrorArchivoCritico;
                    btnFirmar.classList.toggle("disabled", hayErrorArchivoCritico);
                    btnFirmar.title = hayErrorArchivoCritico
                        ? "Archivo no encontrado. Verifique la ruta de red para poder firmar."
                        : "";
                }

                return !hayErroresDeCampos;
            };

            // Manejar intento de envío (Crear / Guardar)
            function manejarIntentoEnvio(e) {
                formularioIntentado = true;
                const esValido = window.actualizarValidaciones();
                if (!esValido) {
                    e.preventDefault();
                    e.stopPropagation();
                    const contenedorErrores = document.getElementById("contenedorErroresUnificado");
                    if (contenedorErrores) contenedorErrores.scrollIntoView({ behavior: 'smooth', block: 'center' });
                }
            }

            // Registrar listeners (una sola vez)
            const btnCrear = document.getElementById("btnCrear");
            const btnModificar = document.getElementById("btnModificar");
            if (btnCrear) btnCrear.addEventListener("click", manejarIntentoEnvio);
            if (btnModificar) btnModificar.addEventListener("click", manejarIntentoEnvio);

            // Actualización en tiempo real al completar campos
            fieldsToValidate.forEach(field => {
                const elem = document.getElementById(field.id);
                if (elem) {
                    elem.addEventListener("change", window.actualizarValidaciones);
                    elem.addEventListener("input", window.actualizarValidaciones);
                }
            });

            const archivoRutaDocValidacion = document.getElementById("archivoRutaDoc");
            if (archivoRutaDocValidacion) {
                archivoRutaDocValidacion.addEventListener("change", window.actualizarValidaciones);
            }

            const adjuntosValidacion = document.getElementById("adjuntos");
            if (adjuntosValidacion) {
                adjuntosValidacion.addEventListener("change", window.actualizarValidaciones);
                adjuntosValidacion.addEventListener("input", window.actualizarValidaciones);
            }

            // Ejecución inicial: deshabilita el botón si hay error de archivo al cargar
            window.actualizarValidaciones();
        });

        function verificarArchivoAlCargar() {
            const id = idHoja?.value;
            const adjunto = document.getElementById('adjuntos')?.value;

            if (!id || !adjunto) {
                return;
            }

            fetch(`/Home/VerificarArchivoPrincipal?id=${encodeURIComponent(id)}`)
                .then(response => response.json())
                .then(data => aplicarResultadoVerificacionArchivo(data))
                .catch(() => { });
        }

        function reverificarArchivo(id) {
            const btnVerificar = event.currentTarget;
            const originalHtml = btnVerificar.innerHTML;
            btnVerificar.disabled = true;
            btnVerificar.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Verificando...';

            fetch(`/Home/VerificarArchivoPrincipal?id=${encodeURIComponent(id)}`)
                .then(response => response.json())
                .then(data => {
                    if (data.success) {
                        // 1. Eliminar el item de error de la lista
                        const liError = document.getElementById("errorArchivoFisico");
                        if (liError) liError.remove();

                        // 2. Vaciar lista completa por si hay más items
                        const lista = document.getElementById("listaErrores");
                        if (lista) lista.innerHTML = "";

                        // 3. Ocultar el contenedor rojo directamente (sin depender de scope externo)
                        const contenedor = document.getElementById("contenedorErroresUnificado");
                        if (contenedor) contenedor.classList.add("d-none");

                        // 4. Habilitar el botón de firma
                        const btnFirmar = document.getElementById("btnFirmar");
                        if (btnFirmar) {
                            btnFirmar.disabled = false;
                            btnFirmar.classList.remove("disabled");
                            btnFirmar.title = "";
                        }

                        // 5. Restaurar el botón de verificación
                        btnVerificar.disabled = false;
                        btnVerificar.innerHTML = originalHtml;

                        alert("Archivo encontrado. Ya puede continuar.");
                    } else {
                        // Actualizar el mensaje y la severidad del error existente
                        const liError = document.getElementById("errorArchivoFisico");
                        if (liError) {
                            if (data.severity) liError.dataset.severity = data.severity;
                            const span = liError.querySelector("span");
                            if (span) span.textContent = data.message;
                        }

                        // Deshabilitar botón de firma si el archivo no se encontró
                        if (data.severity === "error") {
                            const btnFirmar = document.getElementById("btnFirmar");
                            if (btnFirmar) {
                                btnFirmar.disabled = true;
                                btnFirmar.classList.add("disabled");
                                btnFirmar.title = "Archivo no encontrado. Verifique la ruta de red para poder firmar.";
                            }
                        }

                        btnVerificar.disabled = false;
                        btnVerificar.innerHTML = originalHtml;
                    }
                })
                .catch(error => {
                    console.error('Error al verificar archivo:', error);
                    btnVerificar.disabled = false;
                    btnVerificar.innerHTML = originalHtml;
                });
        }

        function aplicarResultadoVerificacionArchivo(data) {
            const contenedor = document.getElementById("contenedorErroresUnificado");
            const lista = document.getElementById("listaErrores");
            const btnFirmar = document.getElementById("btnFirmar");
            if (!contenedor || !lista) {
                return;
            }

            let liError = document.getElementById("errorArchivoFisico");

            if (data.success && data.severity !== "warning") {
                if (liError) {
                    liError.remove();
                }

                if (!lista.children.length) {
                    contenedor.classList.add("d-none");
                }

                if (btnFirmar) {
                    btnFirmar.disabled = false;
                    btnFirmar.classList.remove("disabled");
                    btnFirmar.title = "";
                }

                return;
            }

            contenedor.classList.remove("d-none");

            if (!liError) {
                liError = document.createElement("li");
                liError.id = "errorArchivoFisico";
                lista.prepend(liError);
            }

            liError.dataset.severity = data.severity || "error";
            liError.innerHTML = `
                <span>${data.message || 'No pudimos validar el archivo.'}</span>
                <button type="button" class="btn btn-sm btn-outline-danger ms-2 py-0" data-reverificar-archivo="${idHoja?.value || ''}" style="font-size: 0.75rem; vertical-align: middle;">
                    <i class="bi bi-arrow-clockwise"></i> Verificar ahora
                </button>`;

            if (btnFirmar) {
                const bloquearFirma = data.severity === "error";
                btnFirmar.disabled = bloquearFirma;
                btnFirmar.classList.toggle("disabled", bloquearFirma);
                btnFirmar.title = bloquearFirma
                    ? "Archivo no encontrado. Verifique la ruta de red para poder firmar."
                    : "";
            }
        }
    
        function inicializarEventosVista() {
            document.getElementById("Cliente")?.addEventListener("change", function () {
                actualizarCodigoPlataforma(this.value);
            });
            document.getElementById("Sector")?.addEventListener("change", function () {
                onSectorChange(this);
            });
            document.getElementById("IsSindico")?.addEventListener("change", function () {
                toggleSindico(this.value);
            });
            document.getElementById("CodCliente")?.addEventListener("change", function () {
                cargarContratos(this.value);
            });
            document.getElementById("revisoSocioFirmante")?.addEventListener("focus", function () {
                onSocioFirmanteFocus(this);
            });
            document.getElementById("revisoSocioFirmante")?.addEventListener("change", function () {
                onSocioFirmanteChange(this);
            });
            document.getElementById("btnEliminarAdjunto")?.addEventListener("click", eliminarAdjunto);
            document.getElementById("btnAuditoria")?.addEventListener("click", abrirModalAuditoria);
            document.getElementById("btnGuardarAuditoria")?.addEventListener("click", guardarAuditoria);

            document.querySelectorAll("[data-date-toggle]").forEach(function (input) {
                input.addEventListener("focus", function () { this.type = "date"; });
                input.addEventListener("blur", function () {
                    if (this.value === "") {
                        this.type = "text";
                    }
                });
            });

            document.addEventListener("click", function (event) {
                const button = event.target.closest("[data-reverificar-archivo]");
                if (button) {
                    reverificarArchivo(button.dataset.reverificarArchivo || "");
                }
            });
        }

})();
