DOCUMENTS=	picforth.pdf picforth.info picforth.html picforth.txt

OTHERS=		picforth.aux picforth.cp picforth.fn picforth.ky \
		picforth.log picforth.pg picforth.toc picforth.tp \
		picforth.vr

all:: version.texi ${DOCUMENTS}

version.texi:
	echo "@macro version {}" > version.texi
	echo "${RELEASEVERSION}" >> version.texi
	echo "@end macro" >> version.texi

clean::
	rm -f ${DOCUMENTS} ${OTHERS} version.texi

.SUFFIXES:	.texi .html .pdf .info .txt

.texi.html:
	makeinfo -o $@ --html --no-split --number-sections $<

.texi.pdf:
	texi2dvi --pdf $<

.texi.info:
	makeinfo --no-split $<

.texi.txt:
	makeinfo -o $@ --no-split --no-headers $<