DOCUMENTS=	picforth.pdf picforth.info picforth.html picforth.txt

OTHERS=		picforth.aux picforth.cp picforth.fn picforth.ky \
		picforth.log picforth.pg picforth.toc picforth.tp \
		picforth.vr

all:: ${DOCUMENTS}

clean::
	rm -f ${DOCUMENTS} ${OTHERS}

.SUFFIXES:	.texi .html .pdf .info .txt

.texi.html:
	makeinfo -o $@ --html --no-split --number-sections $<

.texi.pdf:
	texi2dvi --pdf $<

.texi.info:
	makeinfo --no-split $<

.texi.txt:
	makeinfo -o $@ --no-split --no-headers $<