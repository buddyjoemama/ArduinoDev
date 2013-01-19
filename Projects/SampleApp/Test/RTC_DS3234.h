/*
 * SomeTest.h
 *
 * Created: 1/6/2013 4:29:20 PM
 *  Author: Brian
 */ 


#ifndef SOMETEST_H_
#define SOMETEST_H_


class RTC_DS3234
{
public:
	RTC_DS3234(int _cs_pin): cs_pin(_cs_pin) {}
	uint8_t begin(void);
	void adjust(const DateTime& dt);
	uint8_t isrunning(void);
	DateTime now();

protected:
	void cs(int _value);

private:
	int cs_pin;
};

#endif /* SOMETEST_H_ */