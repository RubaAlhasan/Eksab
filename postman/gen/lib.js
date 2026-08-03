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

function buildUrl(pathSegments, { queries = [], pathVars = [], baseVar = 'baseUrl' } = {}) {
  const rawPath = pathSegments.join('/');
  const enabledQueries = queries.filter((q) => !q.disabled);
  const rawQuery = enabledQueries.length
    ? '?' + enabledQueries.map((q) => `${q.key}=${encodeURIComponent(q.value)}`).join('&')
    : '';
  return {
    raw: `{{${baseVar}}}/${rawPath}${rawQuery}`,
    host: [`{{${baseVar}}}`],
    path: pathSegments,
    query: queries.length ? queries.map((q) => ({ key: q.key, value: q.value, description: q.description, disabled: q.disabled || false })) : undefined,
    variable: pathVars.length ? pathVars.map((v) => ({ key: v.key, value: v.value, description: v.description })) : undefined,
  };
}

/**
 * Builds a request object (shared shape used both for item.request and for each example's originalRequest).
 */
function buildRequestCore({ method, pathSegments, opts = {}, body = null, formBody = null, auth = 'inherit', headerExtra = [], description = '' }) {
  const request = {
    method,
    header: jsonHeader(headerExtra),
    url: buildUrl(pathSegments, opts),
  };
  if (description) request.description = description;
  if (formBody !== null && formBody !== undefined) {
    request.header = headerExtra; // no Content-Type override needed; Postman sets it for urlencoded
    request.body = { mode: 'urlencoded', urlencoded: formBody.map((f) => ({ key: f.key, value: String(f.value), description: f.description, disabled: !!f.disabled })) };
  } else if (body !== null && body !== undefined) {
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
    body: body === null || body === undefined ? '' : JSON.stringify(body, null, 2),
  };
}

// ---- Standard ABP-style error bodies --------------------------------------------------
// Shapes below are verified against the live host (curl against a real registered user),
// not guessed — see postman/gen/README-verification-notes.md for the raw transcript.
// Two distinct 403 shapes exist and must not be conflated:
//  - ASP.NET Core's own [Authorize]/[Authorize(Permission)] challenge -> EMPTY body, just the status.
//  - An exception thrown from inside application-service code (business rule, ownership check,
//    wrong password, entity-not-found) -> passes through ABP's exception filter -> JSON wrapper.

function validationErrorBody(fields) {
  const bulletList = fields.map((f) => ` - ${f.message}`).join('\r\n');
  return {
    error: {
      code: null,
      message: 'Your request is not valid!',
      details: `The following errors were detected during validation.\r\n${bulletList}\r\n`,
      data: null,
      validationErrors: fields.map((f) => ({ message: f.message, members: [f.member] })),
    },
  };
}

// Empty body — matches real 401 (missing/invalid Bearer token) and real 403 from a plain
// [Authorize(Permission)] policy failure (verified: GET /api/app/achievement and
// POST /api/app/category both return 403 with zero-length body for a token lacking the permission).
function emptyAuthBody() {
  return null;
}

// Use only for a 403 that is actually THROWN from inside app-service code (AbpAuthorizationException,
// a business-rule check, etc.) — these DO get ABP's standard JSON wrapper, unlike the attribute-based
// 403 above. `message` should be the exact in-code exception message where known.
function businessForbiddenBody(message) {
  return {
    error: { code: null, message, details: null, data: null, validationErrors: null },
  };
}

// Verified shape: `There is no entity {EntityShortName} with id = {id}!`, data/details both null.
function notFoundBody(entityShortName, idExpr) {
  return {
    error: {
      code: null,
      message: `There is no entity ${entityShortName} with id = ${idExpr}!`,
      details: null,
      data: null,
      validationErrors: null,
    },
  };
}

// Generic business-rule violation (insufficient points, already a member, out of stock, ...).
// Verified pattern: unclassified BusinessException/UserFriendlyException in this app maps to 403,
// not 400 (e.g. "Incorrect password." and "Can not find the given email address: ..." both -> 403).
function businessRuleBody(message, data = null) {
  return {
    error: { code: null, message, details: null, data, validationErrors: null },
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
    name, method, pathSegments, opts = {}, body = null, formBody = null, auth = 'inherit', description = '',
    success, errors = [], includeValidation = false, validationFields = [],
    include401 = auth !== 'noauth', include403 = false, permission = '',
    includeNotFound = false, notFoundEntity = '', notFoundIdExpr = '',
    testScriptLines = [], preRequestLines = [],
  } = p;

  const request = buildRequestCore({ method, pathSegments, opts, body, formBody, auth, description });
  const responses = [];

  responses.push(exampleResponse({ name: success.name || '200 OK / Success', status: success.status, code: success.code, forRequest: request, body: success.body }));

  if (includeValidation && validationFields.length) {
    responses.push(exampleResponse({ name: '400 Bad Request - Validation Error', status: 'Bad Request', code: 400, forRequest: request, body: validationErrorBody(validationFields) }));
  }
  if (include401) {
    responses.push(exampleResponse({ name: '401 Unauthorized (missing/invalid Bearer token)', status: 'Unauthorized', code: 401, forRequest: request, body: emptyAuthBody() }));
  }
  if (include403) {
    responses.push(exampleResponse({ name: `403 Forbidden (missing permission: ${permission})`, status: 'Forbidden', code: 403, forRequest: request, body: emptyAuthBody() }));
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
  validationErrorBody, emptyAuthBody, businessForbiddenBody, notFoundBody, businessRuleBody,
  item, folder, uid,
};
