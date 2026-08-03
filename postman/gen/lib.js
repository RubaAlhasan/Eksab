'use strict';

/* Shared helpers for building Postman Collection v2.1 JSON for the Eksabli platform.
   Used by mobile.js / business.js / admin.js generators. */

let uidCounter = 1;
function uid() {
  // deterministic pseudo-uuid so re-runs are diffable
  uidCounter += 1;
  return `00000000-0000-4000-8000-${String(uidCounter).padStart(12, '0')}`;
}

function jsonHeader(extra = []) {
  return [{ key: 'Content-Type', value: 'application/json', type: 'text' }, ...extra];
}

function pathVar(key, value, description) {
  return { key, value: String(value), description };
}

function query(key, value, description, disabled = false) {
  return { key, value: value === undefined ? '' : String(value), description, disabled };
}

function buildUrl(pathSegments, { queries = [], pathVars = [] } = {}) {
  const rawPath = pathSegments.join('/');
  const enabledQueries = queries.filter((q) => !q.disabled);
  const rawQuery = enabledQueries.length
    ? '?' + enabledQueries.map((q) => `${q.key}=${encodeURIComponent(q.value)}`).join('&')
    : '';
  return {
    raw: `{{baseUrl}}/${rawPath}${rawQuery}`,
    host: ['{{baseUrl}}'],
    path: pathSegments,
    query: queries.length ? queries.map((q) => ({ key: q.key, value: q.value, description: q.description, disabled: q.disabled || false })) : undefined,
    variable: pathVars.length ? pathVars.map((v) => ({ key: v.key, value: v.value, description: v.description })) : undefined,
  };
}

/**
 * Builds a request object (shared shape used both for item.request and for each example's originalRequest).
 */
function buildRequestCore({ method, pathSegments, opts = {}, body = null, auth = 'inherit', headerExtra = [], description = '' }) {
  const request = {
    method,
    header: jsonHeader(headerExtra),
    url: buildUrl(pathSegments, opts),
  };
  if (description) request.description = description;
  if (body !== null && body !== undefined) {
    request.body = { mode: 'raw', raw: JSON.stringify(body, null, 2), options: { raw: { language: 'json' } } };
  }
  if (auth === 'noauth') {
    request.auth = { type: 'noauth' };
  } else if (auth === 'bearer') {
    request.auth = { type: 'bearer', bearer: [{ key: 'token', value: '{{accessToken}}', type: 'string' }] };
  }
  // 'inherit' -> omit auth so it inherits from collection-level bearer auth
  return request;
}

function exampleResponse({ name, status, code, forRequest, body, headerExtra = [] }) {
  return {
    id: uid(),
    name,
    originalRequest: forRequest,
    status,
    code,
    _postman_previewlanguage: 'json',
    header: jsonHeader(headerExtra).map((h) => ({ key: h.key, value: h.value })),
    cookie: [],
    body: JSON.stringify(body, null, 2),
  };
}

// ---- Standard ABP-style error bodies -------------------------------------------------

function validationErrorBody(fields) {
  return {
    error: {
      code: 'Volo.Abp.Validation:400001',
      message: 'Your request is not valid!',
      details: null,
      data: {},
      validationErrors: fields.map((f) => ({ message: f.message, members: [f.member] })),
    },
  };
}

function unauthorizedBody() {
  return {
    error: {
      code: null,
      message: 'Unauthorized',
      details: 'You are not authenticated (sign in) in order to perform this operation. Provide a valid Bearer {{accessToken}}.',
      data: {},
      validationErrors: null,
    },
  };
}

function forbiddenBody(permission) {
  return {
    error: {
      code: 'Volo.Abp.Authorization:403001',
      message: 'You are not authorized to perform this operation.',
      details: `Required permission(s): ${permission}`,
      data: {},
      validationErrors: null,
    },
  };
}

function notFoundBody(entityType, idExpr) {
  return {
    error: {
      code: 'Volo.Abp.EntityFrameworkCore:404001',
      message: 'The requested resource could not be found.',
      details: `There is no such an entity. Entity type: Eksabli.${entityType}, id: ${idExpr}`,
      data: {},
      validationErrors: null,
    },
  };
}

function conflictBody(message) {
  return {
    error: {
      code: 'Eksabli:409001',
      message: 'Conflict.',
      details: message,
      data: {},
      validationErrors: null,
    },
  };
}

/**
 * Builds a full Postman item (request) with a standard set of example responses.
 *
 * @param {object} p
 * @param {string} p.name
 * @param {string} p.method
 * @param {string[]} p.pathSegments  e.g. ['stores', ':id']
 * @param {object}  [p.opts]        { queries: [query(...)], pathVars: [pathVar(...)] }
 * @param {object}  [p.body]        request body object (JSON)
 * @param {'inherit'|'bearer'|'noauth'} [p.auth]
 * @param {string}  [p.description]
 * @param {object}  p.success       { status, code, body } - the 2xx example
 * @param {object[]} [p.errors]     extra pre-built error example descriptors: { name, status, code, body }
 * @param {boolean} [p.includeValidation]
 * @param {object[]} [p.validationFields]  [{message, member}]
 * @param {boolean} [p.includeAuthErrors]  include 401/403 examples (skip for noauth endpoints)
 * @param {string}  [p.permission]  permission name shown in the 403 example
 * @param {boolean} [p.includeNotFound]
 * @param {string}  [p.notFoundEntity]
 * @param {string}  [p.notFoundIdExpr]
 * @param {object[]} [p.testScriptLines] raw JS lines appended to the item's test script
 * @param {string[]} [p.preRequestLines] raw JS lines appended to the item's pre-request script
 */
function item(p) {
  const {
    name, method, pathSegments, opts = {}, body = null, auth = 'inherit', description = '',
    success, errors = [], includeValidation = false, validationFields = [],
    includeAuthErrors = auth !== 'noauth', permission = '',
    includeNotFound = false, notFoundEntity = '', notFoundIdExpr = '',
    testScriptLines = [], preRequestLines = [],
  } = p;

  const request = buildRequestCore({ method, pathSegments, opts, body, auth, description });
  const responses = [];

  responses.push(exampleResponse({ name: success.name || '200 OK / Success', status: success.status, code: success.code, forRequest: request, body: success.body }));

  if (includeValidation && validationFields.length) {
    responses.push(exampleResponse({ name: '400 Bad Request - Validation Error', status: 'Bad Request', code: 400, forRequest: request, body: validationErrorBody(validationFields) }));
  }
  if (includeAuthErrors) {
    responses.push(exampleResponse({ name: '401 Unauthorized', status: 'Unauthorized', code: 401, forRequest: request, body: unauthorizedBody() }));
    responses.push(exampleResponse({ name: '403 Forbidden', status: 'Forbidden', code: 403, forRequest: request, body: forbiddenBody(permission || 'Eksabli - relevant permission for this action') }));
  }
  if (includeNotFound) {
    responses.push(exampleResponse({ name: '404 Not Found', status: 'Not Found', code: 404, forRequest: request, body: notFoundBody(notFoundEntity, notFoundIdExpr) }));
  }
  for (const e of errors) {
    responses.push(exampleResponse({ name: e.name, status: e.status, code: e.code, forRequest: request, body: e.body }));
  }

  const events = [];
  if (preRequestLines.length) {
    events.push({ listen: 'prerequest', script: { type: 'text/javascript', exec: preRequestLines } });
  }
  if (testScriptLines.length) {
    events.push({ listen: 'test', script: { type: 'text/javascript', exec: testScriptLines } });
  }

  const out = { name, request, response: responses };
  if (events.length) out.event = events;
  return out;
}

function folder(name, description, items) {
  return { name, description, item: items };
}

module.exports = {
  jsonHeader, pathVar, query, buildUrl, buildRequestCore, exampleResponse,
  validationErrorBody, unauthorizedBody, forbiddenBody, notFoundBody, conflictBody,
  item, folder, uid,
};
