variable checksum
: parity-error ;
: x checksum @ 1 and if parity-error exit then ;
: y porta 3 high? if 1+ then ;