#ifndef READER_GUARD_POLICY_H
#define READER_GUARD_POLICY_H
#include <stdbool.h>
#include <stdint.h>
#include <string.h>

typedef struct {
    uint64_t bytesRead, bytesWritten, reads, writes;
} RGSample;
typedef struct {
    bool hasBaseline;
    uint64_t mediaID;
    RGSample sample;
    double idleSince, lastSample;
} RGIdle;

static inline bool RGSampleEqual(RGSample a, RGSample b) {
    return a.bytesRead == b.bytesRead && a.bytesWritten == b.bytesWritten &&
           a.reads == b.reads && a.writes == b.writes;
}
static inline void RGReset(RGIdle *state) { memset(state, 0, sizeof(*state)); }
static inline bool RGObserve(RGIdle *state, RGSample current, uint64_t mediaID,
                             double now, unsigned minutes, bool valid) {
    if (!valid || !mediaID || minutes < 1 || minutes > 120) { RGReset(state); return false; }
    if (!state->hasBaseline || state->mediaID != mediaID ||
        !RGSampleEqual(state->sample, current) || now < state->lastSample ||
        now - state->lastSample > 30.0) state->idleSince = now;
    state->hasBaseline = true;
    state->mediaID = mediaID;
    state->sample = current;
    state->lastSample = now;
    return now - state->idleSince >= minutes * 60.0;
}
#endif
