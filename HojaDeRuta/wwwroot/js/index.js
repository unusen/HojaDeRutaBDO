(() => {
    const configElement = document.getElementById("indexConfig");
    if (!configElement) {
        return;
    }

    const config = JSON.parse(configElement.textContent);
const estadoMap = config.estados;
        const currentEmpleado = config.currentEmpleado;
        const indexState = {
            pageNumber: 1,
            pageSize: parseInt(document.getElementById('inputPageSize').value || '10', 10),
            sortField: 'Numero',
            sortDirection: 'asc',
            pendientes: config.pendientes,
            numero: '',
            cliente: '',
            estado: '',
            sector: '',
            socio: '',
            fechaDesde: '',
            fechaHasta: ''
        };

        const indexCache = new Map();
        let activeAbortController = null;
        let debounceHandle = null;
        let lastResponse = null;

        document.addEventListener('DOMContentLoaded', function () {
            wireIndexEvents();
            fetchIndexData(true);
        });

        function wireIndexEvents() {
            document.querySelectorAll('.sortable').forEach(header => {
                header.addEventListener('click', function () {
                    const field = this.dataset.sort;
                    if (!field) {
                        return;
                    }

                    if (indexState.sortField === field) {
                        indexState.sortDirection = indexState.sortDirection === 'asc' ? 'desc' : 'asc';
                    } else {
                        indexState.sortField = field;
                        indexState.sortDirection = 'asc';
                    }

                    indexState.pageNumber = 1;
                    fetchIndexData(true);
                });
            });

            bindDebouncedInput('filtroNumero', 'numero');
            bindDebouncedInput('filtroCliente', 'cliente');
            bindDebouncedInput('filtroSector', 'sector');
            bindDebouncedInput('filtroSocio', 'socio');

            bindImmediateInput('filtroEstado', 'estado');
            bindImmediateInput('filtroDesde', 'fechaDesde');
            bindImmediateInput('filtroHasta', 'fechaHasta');

            document.getElementById('inputPageSize').addEventListener('change', function () {
                const nextSize = parseInt(this.value || '10', 10);
                indexState.pageSize = Number.isFinite(nextSize) && nextSize > 0 ? nextSize : 10;
                this.value = indexState.pageSize;
                indexState.pageNumber = 1;
                fetchIndexData(true);
            });

            document.getElementById('inputGoToPage').addEventListener('change', function () {
                const requestedPage = parseInt(this.value || '1', 10);
                indexState.pageNumber = Number.isFinite(requestedPage) && requestedPage > 0 ? requestedPage : 1;
                fetchIndexData(true);
            });

            document.getElementById('btnResetFilters').addEventListener('click', resetFilters);
            document.getElementById('btnTogglePendientes').addEventListener('click', togglePendientes);
        }

        function bindDebouncedInput(elementId, stateKey) {
            const element = document.getElementById(elementId);
            element.addEventListener('input', function () {
                indexState[stateKey] = this.value.trim();
                indexState.pageNumber = 1;
                window.clearTimeout(debounceHandle);
                debounceHandle = window.setTimeout(() => fetchIndexData(true), 300);
            });
        }

        function bindImmediateInput(elementId, stateKey) {
            const element = document.getElementById(elementId);
            element.addEventListener('change', function () {
                indexState[stateKey] = this.value.trim();
                indexState.pageNumber = 1;
                fetchIndexData(true);
            });
        }

        function resetFilters() {
            ['filtroNumero', 'filtroCliente', 'filtroEstado', 'filtroSector', 'filtroSocio', 'filtroDesde', 'filtroHasta']
                .forEach(id => document.getElementById(id).value = '');

            indexState.numero = '';
            indexState.cliente = '';
            indexState.estado = '';
            indexState.sector = '';
            indexState.socio = '';
            indexState.fechaDesde = '';
            indexState.fechaHasta = '';
            indexState.pageNumber = 1;
            fetchIndexData(true);
        }

        function togglePendientes() {
            indexState.pendientes = !indexState.pendientes;
            indexState.pageNumber = 1;
            document.getElementById('btnTogglePendientes').innerHTML = `<i class="bi bi-eye"></i> ${indexState.pendientes ? 'Mostrar todas' : 'Mostrar pendientes'}`;
            fetchIndexData(true);
        }

        function goToPage(pageNumber) {
            if (!lastResponse) {
                return;
            }

            const totalPages = Math.max(1, Math.ceil((lastResponse.totalItems || 0) / Math.max(1, indexState.pageSize)));
            if (pageNumber < 1 || pageNumber > totalPages) {
                return;
            }

            indexState.pageNumber = pageNumber;
            document.getElementById('inputGoToPage').value = pageNumber;
            fetchIndexData(true);
        }

        async function fetchIndexData(showLoading) {
            const cacheKey = buildCacheKey();
            const cached = indexCache.get(cacheKey);
            if (cached) {
                renderIndexData(cached);
                prefetchNeighborPages();
                return;
            }

            if (activeAbortController) {
                activeAbortController.abort();
            }

            activeAbortController = new AbortController();
            setLoading(showLoading);
            setStatus('Actualizando resultados...');

            try {
                const response = await fetch(`/Home/GetIndexData?${buildQueryString()}`, {
                    signal: activeAbortController.signal,
                    headers: {
                        'X-Requested-With': 'XMLHttpRequest'
                    }
                });

                if (!response.ok) {
                    throw new Error('No pudimos cargar las hojas de ruta.');
                }

                const payload = await response.json();
                indexCache.set(cacheKey, payload);
                renderIndexData(payload);
                prefetchNeighborPages();
            } catch (error) {
                if (error.name === 'AbortError') {
                    return;
                }

                setStatus('No pudimos actualizar la lista en este momento.');
            } finally {
                setLoading(false);
            }
        }

        function prefetchNeighborPages() {
            if (!lastResponse) {
                return;
            }

            const totalPages = Math.max(1, Math.ceil((lastResponse.totalItems || 0) / Math.max(1, indexState.pageSize)));
            const nextPage = indexState.pageNumber + 1;
            if (nextPage > totalPages) {
                return;
            }

            const nextState = { ...indexState, pageNumber: nextPage };
            const nextKey = JSON.stringify(nextState);
            if (indexCache.has(nextKey)) {
                return;
            }

            fetch(`/Home/GetIndexData?${new URLSearchParams(nextState).toString()}`, {
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            })
                .then(response => response.ok ? response.json() : null)
                .then(payload => {
                    if (payload) {
                        indexCache.set(nextKey, payload);
                    }
                })
                .catch(() => { });
        }

        function renderIndexData(payload) {
            lastResponse = payload;
            renderTable(payload.items || []);
            renderPagination(payload.totalItems || 0);
            renderSortIcons();
            document.getElementById('inputGoToPage').value = indexState.pageNumber;

            const totalItems = payload.totalItems || 0;
            setStatus(`${totalItems} hoja${totalItems === 1 ? '' : 's'} encontrada${totalItems === 1 ? '' : 's'}.`);
            syncUrl();
        }

        function renderTable(items) {
            const tbody = document.getElementById('hojasBody');
            tbody.innerHTML = '';

            if (!items.length) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="8" class="text-center py-4 text-muted">
                            No encontramos hojas de ruta para los filtros seleccionados.
                        </td>
                    </tr>`;
                return;
            }

            items.forEach(h => {
                const id = encodeURIComponent(h.id);
                const manejadorActual = (h.manejador || '').trim();
                const esManejadorActual = manejadorActual !== ''
                    && manejadorActual.localeCompare(currentEmpleado.trim(), undefined, { sensitivity: 'accent' }) === 0;
                const isDisabled = h.estado === 1 || h.estado === 2 || !esManejadorActual;
                const disabledTitle = h.estado === 1 || h.estado === 2
                    ? 'Edición no permitida'
                    : 'No podés editar esta hoja porque no sos el responsable de la etapa actual';

                tbody.insertAdjacentHTML('beforeend', `
                    <tr class="text-nowrap">
                        <td class="text-center align-middle">${h.numero || ''}</td>
                        <td class="text-center align-middle">
                            <div class="text-truncate d-inline-block w-100" style="max-width: 200px;" title="${escapeHtml(h.clienteName || '')}">
                                ${escapeHtml(h.clienteName || '')}
                            </div>
                        </td>
                        <td class="text-center align-middle">
                            <div class="text-truncate d-inline-block w-100" style="max-width: 220px;" title="${escapeHtml(h.nombreGenerico || '')}">
                                ${escapeHtml(h.nombreGenerico || '')}
                            </div>
                        </td>
                        <td class="text-center align-middle">${escapeHtml(h.sector || '')}</td>
                        <td class="text-center align-middle">${escapeHtml(h.socioFirmanteDetalle || '')}</td>
                        <td class="text-center align-middle">${parseFecha(h.fechaDocumento)}</td>
                        <td class="text-center align-middle">
                            <span class="badge ${getBadgeClass(h.estado)}">${getEstadoTexto(h.estado)}</span>
                        </td>
                        <td class="text-center align-middle">
                            <a href="/Home/Upsert?mode=2&id=${id}" class="me-2 text-decoration-none icon-action" title="Ver detalles">
                                <i class="bi bi-eye text-success fs-5"></i>
                            </a>
                            ${isDisabled
                                ? `<span class="me-2 text-muted fs-5" role="button" aria-disabled="true" title="${disabledTitle}"><i class="bi bi-pencil text-black-50 opacity-75"></i></span>`
                                : `<a href="/Home/Upsert?mode=1&id=${id}" class="me-2 text-decoration-none icon-action" title="Editar"><i class="bi bi-pencil text-info fs-5"></i></a>`
                            }
                        </td>
                    </tr>`);
            });
        }

        function renderPagination(totalItems) {
            const totalPages = Math.max(1, Math.ceil(totalItems / Math.max(1, indexState.pageSize)));
            const pagination = document.getElementById('pagination');
            pagination.innerHTML = '';

            if (totalItems === 0) {
                return;
            }

            const maxPagesToShow = 5;
            let startPage = Math.max(1, indexState.pageNumber - Math.floor(maxPagesToShow / 2));
            let endPage = Math.min(totalPages, startPage + maxPagesToShow - 1);

            if (endPage - startPage + 1 < maxPagesToShow) {
                startPage = Math.max(1, endPage - maxPagesToShow + 1);
            }

            addPaginationButton('Anterior', indexState.pageNumber - 1, indexState.pageNumber === 1);

            if (startPage > 1) {
                addPaginationButton('1', 1, false, false);
                pagination.insertAdjacentHTML('beforeend', '<li class="page-item disabled"><span class="page-link">...</span></li>');
            }

            for (let page = startPage; page <= endPage; page++) {
                addPaginationButton(page.toString(), page, false, page === indexState.pageNumber);
            }

            if (endPage < totalPages) {
                pagination.insertAdjacentHTML('beforeend', '<li class="page-item disabled"><span class="page-link">...</span></li>');
                addPaginationButton(totalPages.toString(), totalPages, false, false);
            }

            addPaginationButton('Siguiente', indexState.pageNumber + 1, indexState.pageNumber === totalPages);
        }

        function addPaginationButton(label, pageNumber, disabled, active) {
            const pagination = document.getElementById('pagination');
            const disabledClass = disabled ? 'disabled' : '';
            const activeClass = active ? 'active' : '';
            pagination.insertAdjacentHTML('beforeend', `
                <li class="page-item ${disabledClass} ${activeClass}">
                    <a class="page-link" href="#" data-page="${pageNumber}">${label}</a>
                </li>`);

            pagination.lastElementChild.querySelector('a').addEventListener('click', function (event) {
                event.preventDefault();
                if (!disabled) {
                    goToPage(pageNumber);
                }
            });
        }

        function renderSortIcons() {
            document.querySelectorAll('[id^="sort-icon-"]').forEach(span => {
                const fieldName = span.id.replace('sort-icon-', '');
                if (fieldName === indexState.sortField) {
                    span.textContent = indexState.sortDirection === 'asc' ? ' ▲' : ' ▼';
                    span.parentElement.classList.add('active-sort');
                } else {
                    span.textContent = '';
                    span.parentElement.classList.remove('active-sort');
                }
            });
        }

        function buildQueryString() {
            const params = new URLSearchParams();
            Object.entries(indexState).forEach(([key, value]) => {
                if (value !== '' && value !== null && value !== undefined) {
                    params.set(key, value);
                }
            });

            return params.toString();
        }

        function buildCacheKey() {
            return JSON.stringify(indexState);
        }

        function syncUrl() {
            const params = new URLSearchParams();
            params.set('pendientes', indexState.pendientes);
            history.replaceState(null, '', `${window.location.pathname}?${params.toString()}`);
        }

        function setLoading(isLoading) {
            const loading = document.getElementById('tableLoading');
            loading.classList.toggle('d-none', !isLoading);
            loading.classList.toggle('d-flex', isLoading);
        }

        function setStatus(message) {
            document.getElementById('indexStatus').textContent = message;
        }

        function parseFecha(fecha) {
            if (!fecha) {
                return '';
            }

            const date = new Date(fecha);
            if (Number.isNaN(date.getTime())) {
                return '';
            }

            return date.toLocaleDateString('es-AR');
        }

        function getEstadoTexto(estadoId) {
            const estado = estadoMap.find(e => (e.id ?? e.Id) === estadoId);
            return estado ? (estado.desc ?? estado.Desc) : 'Sin estado';
        }

        function getBadgeClass(estadoId) {
            switch (estadoId) {
                case 0: return 'bg-warning text-dark';
                case 1: return 'bg-success';
                case 2: return 'bg-danger';
                default: return 'bg-secondary';
            }
        }

        function escapeHtml(value) {
            return (value || '')
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;')
                .replaceAll("'", '&#39;');
        }
    
})();
