(() => {
    const initialized = new WeakSet();

    const readTheme = () => {
        const root = getComputedStyle(document.documentElement);
        const body = getComputedStyle(document.body);
        const value = name => root.getPropertyValue(name).trim();

        return {
            bodyBackground: value("--bs-body-bg") || body.backgroundColor,
            bodyColor: value("--bs-body-color") || body.color,
            border: value("--bs-border-color") || body.color,
            primary: value("--bs-primary") || body.color,
            primaryBackground: value("--bs-primary-bg-subtle") || value("--bs-body-bg"),
            success: value("--bs-success") || body.color,
            successBackground: value("--bs-success-bg-subtle") || value("--bs-body-bg"),
            secondary: value("--bs-secondary") || body.color,
            secondaryBackground: value("--bs-secondary-bg-subtle") || value("--bs-body-bg"),
            warning: value("--bs-warning") || body.color,
            monospace: value("--bs-font-monospace") || body.fontFamily,
            fontFamily: body.fontFamily
        };
    };

    const createGraph = canvas => {
        if (initialized.has(canvas)) {
            return;
        }

        initialized.add(canvas);

        if (typeof window.cytoscape !== "function") {
            canvas.textContent = "The graph renderer could not be loaded.";
            return;
        }

        let graph;
        try {
            graph = JSON.parse(canvas.dataset.dependencyGraph || "{}");
        } catch {
            canvas.textContent = "The dependency graph data could not be read.";
            return;
        }

        const nodes = Array.isArray(graph.nodes) ? graph.nodes : [];
        const edges = Array.isArray(graph.edges) ? graph.edges : [];
        if (nodes.length === 0) {
            canvas.textContent = "No dependencies were found in this scope.";
            return;
        }

        const modal = canvas.closest("dialog[data-dependency-graph-modal]");
        canvas.replaceChildren();
        const theme = readTheme();
        const cy = window.cytoscape({
            container: canvas,
            elements: [...nodes, ...edges],
            minZoom: 0.03,
            maxZoom: 8,
            wheelSensitivity: 0.36,
            autolock: false,
            autoungrabify: false,
            boxSelectionEnabled: false,
            style: [
                {
                    selector: "node",
                    style: {
                        "background-color": theme.bodyBackground,
                        "border-color": theme.border,
                        "border-width": 1,
                        color: theme.bodyColor,
                        label: "data(label)",
                        "font-family": theme.monospace,
                        "font-size": 12,
                        "text-wrap": "wrap",
                        "text-max-width": 190,
                        "text-valign": "center",
                        "text-halign": "center",
                        shape: "round-rectangle",
                        padding: 12,
                        width: "label",
                        height: "label"
                    }
                },
                {
                    selector: 'node[kind = "Project"]',
                    style: {
                        "background-color": theme.primaryBackground,
                        "border-color": theme.primary
                    }
                },
                {
                    selector: 'node[kind = "Package"]',
                    style: {
                        "background-color": theme.successBackground,
                        "border-color": theme.success
                    }
                },
                {
                    selector: 'node[kind = "Framework"]',
                    style: {
                        "background-color": theme.secondaryBackground,
                        "border-color": theme.secondary
                    }
                },
                {
                    selector: "edge",
                    style: {
                        "line-color": theme.border,
                        "target-arrow-color": theme.border,
                        "target-arrow-shape": "triangle",
                        "curve-style": "bezier",
                        width: 1.2,
                        opacity: 0.72
                    }
                },
                {
                    selector: 'edge[edgeKind = "ProjectReference"]',
                    style: {
                        "line-color": theme.primary,
                        "target-arrow-color": theme.primary,
                        width: 1.8
                    }
                },
                {
                    selector: 'edge[edgeKind = "DirectPackageReference"]',
                    style: {
                        "line-color": theme.success,
                        "target-arrow-color": theme.success,
                        width: 1.8
                    }
                },
                {
                    selector: 'edge[edgeKind = "TransitivePackageReference"]',
                    style: {
                        "line-style": "dashed"
                    }
                },
                {
                    selector: 'edge[edgeKind = "FrameworkReference"]',
                    style: {
                        "line-style": "dotted",
                        "line-color": theme.secondary,
                        "target-arrow-color": theme.secondary
                    }
                },
                {
                    selector: "edge.is-connected",
                    style: {
                        "line-color": theme.warning,
                        "target-arrow-color": theme.warning,
                        width: 3,
                        opacity: 1
                    }
                },
                {
                    selector: ".is-focused",
                    style: {
                        "border-color": theme.warning,
                        "border-width": 3,
                        opacity: 1
                    }
                },
                {
                    selector: ".is-dimmed",
                    style: {
                        opacity: 0.14
                    }
                }
            ],
            layout: {
                name: "breadthfirst",
                directed: true,
                padding: 16,
                spacingFactor: 0.72,
                avoidOverlap: true,
                animate: false
            }
        });

        const shell = canvas.closest("[data-dependency-graph-shell]");
        const selection = shell?.querySelector("[data-dependency-graph-selection]");
        const filterInputs = shell
            ? Array.from(shell.querySelectorAll("[data-dependency-graph-filter]"))
            : [];

        const clearSelection = () => {
            cy.elements().removeClass("is-focused is-connected is-dimmed");
            if (selection) {
                selection.textContent = "Click a node to inspect its dependencies.";
            }
        };

        const focusNode = node => {
            const neighborhood = node.closedNeighborhood();
            cy.elements().removeClass("is-focused is-connected is-dimmed");
            cy.elements().difference(neighborhood).addClass("is-dimmed");
            node.addClass("is-focused");
            node.connectedEdges().addClass("is-connected");

            if (selection) {
                const data = node.data();
                const version = data.version ? ` ${data.version}` : "";
                const outgoing = node.outgoers("node").filter(element => element.visible()).length;
                const incoming = node.incomers("node").filter(element => element.visible()).length;
                selection.textContent = `${data.name}${version} · ${data.kind} · ${incoming} incoming · ${outgoing} outgoing`;
            }
        };

        const visibleElements = () => cy.elements().filter(element => element.visible());

        const fit = () => {
            cy.resize();
            const elements = visibleElements();
            if (elements.length > 0) {
                cy.fit(elements, 16);
            }
        };

        const resize = () => {
            cy.resize();
        };

        const runLayout = () => {
            cy.layout({
                name: "breadthfirst",
                eles: visibleElements(),
                directed: true,
                padding: 16,
                spacingFactor: 0.72,
                avoidOverlap: true,
                animate: false
            }).run();
        };

        const applyFilters = () => {
            const showIndirect = filterInputs
                .find(input => input.dataset.dependencyGraphFilter === "indirect")
                ?.checked ?? false;
            const showFramework = filterInputs
                .find(input => input.dataset.dependencyGraphFilter === "framework")
                ?.checked ?? true;

            cy.nodes().forEach(node => {
                const kind = node.data("kind");
                const isIndirectPackage = kind === "Package" && node.data("direct") !== true;
                const isFramework = kind === "Framework";
                const visible = (!isIndirectPackage || showIndirect) &&
                    (!isFramework || showFramework);

                if (visible) {
                    node.show();
                } else {
                    node.hide();
                }
            });

            cy.edges().forEach(edge => {
                const visible = edge.source().visible() && edge.target().visible();
                if (visible) {
                    edge.show();
                } else {
                    edge.hide();
                }
            });

            clearSelection();
            runLayout();
            fit();
        };

        const download = () => {
            try {
                const image = cy.png({
                    full: true,
                    bg: theme.bodyBackground,
                    maxWidth: 4096,
                    maxHeight: 4096
                });
                const anchor = document.createElement("a");
                const stamp = new Date().toISOString().replace(/[.:]/g, "-");
                anchor.href = image;
                anchor.download = `lasagna-dependency-graph-${stamp}.png`;
                document.body.appendChild(anchor);
                anchor.click();
                anchor.remove();
            } catch {
                if (selection) {
                    selection.textContent = "The graph image could not be downloaded.";
                }
            }
        };

        cy.on("tap", "node", event => focusNode(event.target));
        cy.on("tap", event => {
            if (event.target === cy) {
                clearSelection();
            }
        });
        cy.on("grab", "node", () => canvas.classList.add("is-dragging"));
        cy.on("free", "node", () => canvas.classList.remove("is-dragging"));

        shell?.addEventListener("click", event => {
            const action = event.target.closest("[data-dependency-graph-action]");
            if (!action) {
                return;
            }

            if (action.dataset.dependencyGraphAction === "fit") {
                fit();
            } else if (action.dataset.dependencyGraphAction === "reset") {
                clearSelection();
                runLayout();
                fit();
            } else if (action.dataset.dependencyGraphAction === "download") {
                download();
            }
        });

        filterInputs.forEach(input => {
            input.addEventListener("change", applyFilters);
        });

        applyFilters();

        if (typeof window.ResizeObserver === "function") {
            new ResizeObserver(() => {
                resize();
                cy.forceRender();
            }).observe(canvas);
        }

        let hasOpened = false;
        modal?.addEventListener("toggle", () => {
            if (modal.open) {
                requestAnimationFrame(() => {
                    resize();
                    if (!hasOpened) {
                        fit();
                        hasOpened = true;
                    }
                    cy.forceRender();
                    requestAnimationFrame(() => {
                        resize();
                        cy.forceRender();
                    });
                });
            }
        });

        if (!modal) {
            fit();
        }
    };

    const initialize = root => {
        if (root.matches?.("[data-dependency-graph]")) {
            createGraph(root);
        }

        root.querySelectorAll?.("[data-dependency-graph]").forEach(createGraph);
    };

    const boot = () => {
        initialize(document);

        new MutationObserver(mutations => {
            mutations.forEach(mutation => {
                mutation.addedNodes.forEach(node => {
                    if (node.nodeType === Node.ELEMENT_NODE) {
                        initialize(node);
                    }
                });
            });
        }).observe(document.body, { childList: true, subtree: true });
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot, { once: true });
    } else {
        boot();
    }
})();
