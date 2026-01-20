namespace FsPlay

module Bootstrap =
  let bootstrapScript = """
(function () {
  if (window.__fsDriver) {
    return true;
  }

// ------------------------------------------------------------
// Find deepest element at (x,y), descending through shadow DOM
// ------------------------------------------------------------
function deepElementFromPoint(x, y, root) {
    if (!root) root = document;

    const el = root.elementFromPoint(x, y);
    if (!el) return null;

    if (el.shadowRoot) {
        const deeper = deepElementFromPoint(x, y, el.shadowRoot);
        return deeper || el;
    }
    return el;
}

// ------------------------------------------------------------
// Determine whether an element lives inside a shadow DOM
// ------------------------------------------------------------
function isInsideShadow(el) {
    let node = el;
    while (node) {
        if (node instanceof ShadowRoot) return true;
        node = node.parentNode;
    }
    return false;
}

// ------------------------------------------------------------
// Find nearest scrollable ancestor (including shadow hosts)
// Special case: Ionic ion-content → shadowRoot → .inner-scroll
// ------------------------------------------------------------
function findScrollableParent(el) {
    let node = el;

    while (node && node !== document) {

        // Ionic special case
        if (node.tagName === "ION-CONTENT" && node.shadowRoot) {
            const inner = node.shadowRoot.querySelector(".inner-scroll");
            if (inner) return inner;
        }

        // Generic scrollable container check
        const style = window.getComputedStyle(node);
        const overflow = style.overflow;
        const overflowY = style.overflowY;

        const scrollable =
            (overflow === "auto" || overflow === "scroll" ||
             overflowY === "auto" || overflowY === "scroll");

        if (scrollable && node.scrollHeight > node.clientHeight) {
            return node;
        }

        // Shadow DOM boundary
        if (node.parentNode instanceof ShadowRoot) {
            node = node.parentNode.host;
        } else {
            node = node.parentNode;
        }
    }

    // Fallback to document scroll element
    return document.scrollingElement || document.documentElement;
}

// ------------------------------------------------------------
// Scroll a specific target element
// ------------------------------------------------------------
function performScroll(target, scrollX, scrollY) {
  if (!target) return "no-scroll-target";

  const normalizeDelta = (value) => {
    if (typeof value === "number") return Number.isFinite(value) ? value : 0;
    if (typeof value === "string") {
      const parsed = parseFloat(value);
      return Number.isFinite(parsed) ? parsed : 0;
    }
    return 0;
  };

  const deltaX = normalizeDelta(scrollX);
  const deltaY = normalizeDelta(scrollY);

  if (typeof target.scrollBy === "function") {
    target.scrollBy({ left: deltaX, top: deltaY, behavior: "smooth" });
  } else {
    const nextLeft = (target.scrollLeft || 0) + deltaX;
    const nextTop = (target.scrollTop || 0) + deltaY;
    target.scrollTo({ left: nextLeft, top: nextTop, behavior: "smooth" });
  }

  return "scrolled";
}

// ------------------------------------------------------------
// PUBLIC API: scrollByPoint(x, y, scrollX, scrollY)
// ------------------------------------------------------------
function scrollByPoint(x, y, scrollX, scrollY) {
    const el = deepElementFromPoint(x, y);
    if (!el) {
        return {
            error: "no-element-under-point"
        };
    }

    const insideShadow = isInsideShadow(el);
    const scrollTarget = findScrollableParent(el);
    const result = performScroll(scrollTarget, scrollX, scrollY);

    return {
        elementUnderPoint: el.tagName,
        insideShadowDOM: insideShadow,
        scrollTarget: scrollTarget ? (scrollTarget.tagName || "shadow-root") : null,
        result: result
    };
}


  function isEditableElement(element) {
    if (!element) return false;
    if (element.isContentEditable) return true;
    if (typeof element.value === 'string' && element.readOnly !== true) return true;
    return false;
  }

  function resolveTypingTarget() {
    const active = document.activeElement;
    if (isEditableElement(active)) {
      return active;
    }
    const shadowRoot = active && active.shadowRoot;
    if (shadowRoot && isEditableElement(shadowRoot.activeElement)) {
      return shadowRoot.activeElement;
    }
    return null;
  }

  const focusableSelector = 'input, textarea, select, button, [contenteditable], [tabindex]';

  function isFocusable(element) {
    if (!element || typeof element !== 'object') return false;
    if (element.disabled || element.getAttribute?.('disabled') !== null) return false;
    if (element.isContentEditable) return true;
    if (typeof element.tabIndex === 'number' && element.tabIndex >= 0) return true;
    const tag = element.tagName;
    return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || tag === 'BUTTON';
  }

  function findFocusable(element) {
    if (!element) return null;
    if (isFocusable(element)) return element;
    if (element.shadowRoot) {
      const active = element.shadowRoot.activeElement;
      if (isFocusable(active)) return active;
      const shadowDesc = element.shadowRoot.querySelector(focusableSelector);
      if (isFocusable(shadowDesc)) return shadowDesc;
    }
    if (element.matches?.('label[for]')) {
      const targetId = element.getAttribute('for');
      if (targetId) {
        const labelled = document.getElementById(targetId);
        if (isFocusable(labelled)) return labelled;
      }
    }
    const fallback = element.querySelector?.(focusableSelector);
    if (isFocusable(fallback)) return fallback;
    return null;
  }

  function ensureFocus(element) {
    const focusTarget = findFocusable(element);
    if (!focusTarget || typeof focusTarget.focus !== 'function') return;
    try {
      focusTarget.focus({ preventScroll: true });
    } catch (err) {
      try {
        focusTarget.focus();
      } catch (e) { /* ignore */ }
    }
  }

  // call: await webView.EvaluateJavaScriptAsync($"({typeIntoActiveElement.toString()})('{text}', {delay})");
function typeIntoActiveElement(text, delayMs = 10) {
  const el = resolveTypingTarget();
  if (!el) return false;
  const str = String(text ?? "");

  // helper to set value via prototype setter if available
  function setValueWithSetter(element, newVal) {
    if (!('value' in element)) { element.textContent = newVal; return; }
    const proto = Object.getPrototypeOf(element);
    const desc = Object.getOwnPropertyDescriptor(proto, 'value');
    if (desc && desc.set) {
      desc.set.call(element, newVal);
    } else {
      element.value = newVal;
    }
  }

  // send events for a single character
  function sendCharEvents(element, ch) {
    const key = ch.length === 1 ? ch : 'Unidentified';
    try {
      element.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key }));
      element.dispatchEvent(new KeyboardEvent('keypress', { bubbles: true, cancelable: true, key }));
    } catch (e) { /* ignore */ }

    // update value using selection/caret if possible
    if ('value' in element) {
      const start = element.selectionStart ?? element.value.length;
      const end = element.selectionEnd ?? element.value.length;
      const before = element.value.substring(0, start);
      const after = element.value.substring(end);
      setValueWithSetter(element, before + ch + after);
      const pos = before.length + ch.length;
      if (element.setSelectionRange) element.setSelectionRange(pos, pos);
    } else if (element.isContentEditable) {
      element.textContent = (element.textContent ?? "") + ch;
    } else {
      return;
    }

    // InputEvent with data and inputType
    try {
      const ie = new InputEvent('input', {
        bubbles: true, cancelable: true, composed: true,
        inputType: 'insertText', data: ch
      });
      element.dispatchEvent(ie);
    } catch (e) {
      const ev = document.createEvent('Event');
      ev.initEvent('input', true, true);
      element.dispatchEvent(ev);
    }

    try {
      element.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, cancelable: true, key }));
    } catch (e) { /* ignore */ }
  }

  // type each character with optional delay
  return new Promise(resolve => {
    let i = 0;
    function step() {
      if (i >= str.length) {
        // final change event to commit
        el.dispatchEvent(new Event('change', { bubbles: true, cancelable: true }));
        resolve(true);
        return;
      }
      sendCharEvents(el, str[i]);
      i++;
      if (!delayMs) step();
      else setTimeout(step, delayMs);
    }
    step();
  });
}

  function toClientPoint(pageX, pageY) {
    const zoom = 1;
    let adjustedX = pageX;
    let adjustedY = pageY;
    if (pageX > window.innerWidth || pageY > window.innerHeight) {
      adjustedX = pageX / zoom;
      adjustedY = pageY / zoom;
    }

    const clientX = adjustedX - window.scrollX;
    const clientY = adjustedY - window.scrollY;

    return {
      clientX: Math.round(clientX),
      clientY: Math.round(clientY)
    };
  }

  function clampToViewport(clientPoint) {
    const width = window.innerWidth;
    const height = window.innerHeight;
    return {
      clientX: Math.min(Math.max(clientPoint.clientX, 0), width - 1),
      clientY: Math.min(Math.max(clientPoint.clientY, 0), height - 1)
    };
  }

  function mouseInit(clientPoint, buttonCode, buttonsMask) {
    return {
      clientX: clientPoint.clientX,
      clientY: clientPoint.clientY,
      button: buttonCode || 0,
      buttons: typeof buttonsMask === 'number' ? buttonsMask : 1,
      bubbles: true,
      cancelable: true,
      composed: true,
      view: window
    };
  }

  function fireMouse(target, type, init) {
    const event = new MouseEvent(type, init);
    target.dispatchEvent(event);
  }

  window.__fsDriver = {
    clickAt: function (pageX, pageY, buttonCode) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = deepElementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }

      const init = mouseInit(client, buttonCode || 0, 1);
      fireMouse(element, 'mousemove', init);
      fireMouse(element, 'mousedown', init);
      ensureFocus(element);
      fireMouse(element, 'mouseup', mouseInit(client, buttonCode || 0, 0));
      fireMouse(element, 'click', init);
      return true;
    },

    doubleClick: function (pageX, pageY) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = deepElementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }
      const init = mouseInit(client, 0, 1);
      fireMouse(element, 'mousemove', init);
      fireMouse(element, 'mousedown', init);
      ensureFocus(element);
      fireMouse(element, 'mouseup', mouseInit(client, 0, 0));
      fireMouse(element, 'click', init);
      fireMouse(element, 'dblclick', init);
      return true;
    },

    moveTo: function (pageX, pageY) {
      const client = clampToViewport(toClientPoint(pageX, pageY));
      const element = deepElementFromPoint(client.clientX, client.clientY);
      if (!element) {
        return false;
      }
      fireMouse(element, 'mousemove', mouseInit(client, 0, 0));
      return true;
    },

    dragDrop: function (startX, startY, endX, endY) {
      const start = clampToViewport(toClientPoint(startX, startY));
      const end = clampToViewport(toClientPoint(endX, endY));
      const source = deepElementFromPoint(start.clientX, start.clientY);
      const target = deepElementFromPoint(end.clientX, end.clientY);
      if (!source || !target) {
        return false;
      }

      const startInit = mouseInit(start, 0, 1);
      const moveInit = mouseInit(end, 0, 1);
      const upInit = mouseInit(end, 0, 0);

      fireMouse(source, 'mousemove', startInit);
      fireMouse(source, 'mousedown', startInit);
      fireMouse(source, 'mousemove', moveInit);
      fireMouse(target, 'mousemove', moveInit);
      fireMouse(target, 'mouseup', upInit);
      fireMouse(target, 'drop', upInit);
      fireMouse(source, 'mouseup', upInit);
      return true;
    },

    scrollBy: function (deltaX, deltaY) {
      window.scrollBy(deltaX || 0, deltaY || 0);
      return { scrollX: window.scrollX, scrollY: window.scrollY };
    },

    scrollTo: function (x, y) {
      window.scrollTo(x || 0, y || 0);
      return { scrollX: window.scrollX, scrollY: window.scrollY };
    },

    touchScroll: function (startX, startY, scrollX, scrollY) {
      return scrollByPoint(startX,startY,scrollX,scrollY);
    },

    typeText: function (text) {
      const target = resolveTypingTarget();
      if (!target) {
        return false;
      }
      typeIntoActiveElement(text);
      return true;
    },

    pressKey: function (key, modifiers) {
      const target = document.activeElement || document.body || document.documentElement;
      const init = Object.assign(
        {
          key: key,
          code: key,
          keyCode: key === ' ' ? 32 : key?.length === 1 ? key.toUpperCase().charCodeAt(0) : 0,
          which: key === ' ' ? 32 : key?.length === 1 ? key.toUpperCase().charCodeAt(0) : 0,
          bubbles: true,
          cancelable: true
        },
        modifiers || {}
      );
      try {
        target.dispatchEvent(new KeyboardEvent('keydown', init));
        target.dispatchEvent(new KeyboardEvent('keypress', init));
        target.dispatchEvent(new KeyboardEvent('keyup', init));
        if (key === 'Enter') {
          target.dispatchEvent(new Event('change', { bubbles: true }));
        }
        return true;
      } catch (err) {
        console.error(err);
        return false;
      }
    },

    meta: function () {
      return {
        scrollX: window.scrollX,
        scrollY: window.scrollY,
        width: window.innerWidth,
        height: window.innerHeight,
        url: window.location.href || null,
        title: document.title || null
      };
    },

    navigate: function (targetUrl) {
      if (!targetUrl) {
        return false;
      }
      if (window.location.href === targetUrl) {
        window.location.reload();
      } else {
        window.location.href = targetUrl;
      }
      return true;
    },
  };

  return true;
})();
"""
